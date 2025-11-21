using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Mubai.UnitOfWork.Abstractions;
using Mubai.UnitOfWork.EntityFrameworkCore;
using System.Reflection;
using Xunit;

namespace Mubai.UnitOfWork.Tests
{
    public class EfUnitOfWorkTests
    {
        [Fact]
        public async Task BeginTransactionAsync_ReturnsFalse_ForNonRelationalProvider()
        {
            var options = new DbContextOptionsBuilder<TestDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            await using var context = new TestDbContext(options);
            await using var unitOfWork = new EfUnitOfWork<TestDbContext>(context);

            var started = await unitOfWork.BeginTransactionAsync();

            started.Should().BeFalse();
            context.Database.CurrentTransaction.Should().BeNull();
        }

        [Fact]
        public async Task BeginTransactionAsync_ReturnsFalse_WhenTransactionAlreadyExists()
        {
            var ctx = CreateRelationalContext();
            using var connection = ctx.Connection;
            await using var context = new TestDbContext(ctx.Options);
            await using var unitOfWork = new EfUnitOfWork<TestDbContext>(context);

            var firstStart = await unitOfWork.BeginTransactionAsync();
            var secondStart = await unitOfWork.BeginTransactionAsync();

            firstStart.Should().BeTrue();
            secondStart.Should().BeFalse();
            context.Database.CurrentTransaction.Should().NotBeNull();
        }

        [Fact]
        public async Task CommitAsync_WhenTransactionActive_ClearsTransaction()
        {
            var ctx = CreateRelationalContext();
            using var connection = ctx.Connection;
            await using var context = new TestDbContext(ctx.Options);
            await using var unitOfWork = new EfUnitOfWork<TestDbContext>(context);

            await unitOfWork.BeginTransactionAsync();
            context.Database.CurrentTransaction.Should().NotBeNull();

            await unitOfWork.CommitAsync();

            context.Database.CurrentTransaction.Should().BeNull();
        }

        [Fact]
        public async Task CommitAsync_WithNoTransaction_DoesNothing()
        {
            var ctx = CreateRelationalContext();
            using var connection = ctx.Connection;
            await using var context = new TestDbContext(ctx.Options);
            await using var unitOfWork = new EfUnitOfWork<TestDbContext>(context);

            await unitOfWork.CommitAsync();

            context.Database.CurrentTransaction.Should().BeNull();
        }

        [Fact]
        public async Task RollbackAsync_WhenTransactionActive_ClearsTransaction()
        {
            var ctx = CreateRelationalContext();
            using var connection = ctx.Connection;
            await using var context = new TestDbContext(ctx.Options);
            await using var unitOfWork = new EfUnitOfWork<TestDbContext>(context);

            await unitOfWork.BeginTransactionAsync();

            await unitOfWork.RollbackAsync();

            context.Database.CurrentTransaction.Should().BeNull();
        }

        [Fact]
        public async Task ExecuteInTransactionAsync_CommitsOperationsOnSuccess()
        {
            var ctx = CreateRelationalContext();
            using var connection = ctx.Connection;
            await using var context = new TestDbContext(ctx.Options);
            await using var unitOfWork = new EfUnitOfWork<TestDbContext>(context);

            await unitOfWork.ExecuteInTransactionAsync(async _ =>
            {
                context.Entities.Add(new TestEntity { Name = "committed" });
                await context.SaveChangesAsync();
            });

            await using var verificationContext = new TestDbContext(ctx.Options);
            var count = await verificationContext.Entities.CountAsync();

            count.Should().Be(1);
            verificationContext.Database.CurrentTransaction.Should().BeNull();
        }

        [Fact]
        public async Task ExecuteInTransactionAsync_RollsBackOperationsOnException()
        {
            var ctx = CreateRelationalContext();
            using var connection = ctx.Connection;
            await using var context = new TestDbContext(ctx.Options);
            await using var unitOfWork = new EfUnitOfWork<TestDbContext>(context);

            var action = async () => await unitOfWork.ExecuteInTransactionAsync(async _ =>
            {
                context.Entities.Add(new TestEntity { Name = "should-rollback" });
                await context.SaveChangesAsync();
                throw new InvalidOperationException("force rollback");
            });

            await action.Should().ThrowAsync<InvalidOperationException>();

            await using var verificationContext = new TestDbContext(ctx.Options);
            var count = await verificationContext.Entities.CountAsync();

            count.Should().Be(0);
            verificationContext.Database.CurrentTransaction.Should().BeNull();
        }

        [Fact]
        public async Task SaveChangesAsync_ThrowsWhenCancelled()
        {
            var ctx = CreateRelationalContext();
            using var connection = ctx.Connection;
            await using var context = new TestDbContext(ctx.Options);
            await using var unitOfWork = new EfUnitOfWork<TestDbContext>(context);
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            context.Entities.Add(new TestEntity { Name = "cancelled" });

            var action = () => unitOfWork.SaveChangesAsync(cts.Token);

            await action.Should().ThrowAsync<OperationCanceledException>();
        }

        [Fact]
        public async Task CreateDisposeLoop_DoesNotLeakAndPersistsData()
        {
            const string connectionString = "Data Source=loop;Mode=Memory;Cache=Shared";
            using var keeper = new SqliteConnection(connectionString);
            keeper.Open();

            var options = new DbContextOptionsBuilder<TestDbContext>()
                .UseSqlite(connectionString)
                .Options;

            using (var setup = new TestDbContext(options))
            {
                setup.Database.EnsureCreated();
            }

            var iterations = 500;
            for (var i = 0; i < iterations; i++)
            {
                await using var context = new TestDbContext(options);
                await using var unitOfWork = new EfUnitOfWork<TestDbContext>(context);

                await unitOfWork.ExecuteInTransactionAsync(async _ =>
                {
                    context.Entities.Add(new TestEntity { Name = $"bulk-{i}" });
                    await context.SaveChangesAsync();
                });
            }

            await using var verification = new TestDbContext(options);
            (await verification.Entities.CountAsync()).Should().Be(iterations);
        }

        [Fact]
        public async Task ConcurrentTransactions_OnSameRow_OneSucceedsOneFailsAndRollsBack()
        {
            const string connectionString = "Data Source=lock-test;Mode=Memory;Cache=Shared";
            using var keeper = new SqliteConnection(connectionString);
            keeper.Open();

            var options = new DbContextOptionsBuilder<TestDbContext>()
                .UseSqlite(connectionString)
                .Options;

            using (var setup = new TestDbContext(options))
            {
                setup.Database.EnsureCreated();
                setup.Entities.Add(new TestEntity { Name = "seed" });
                setup.SaveChanges();
            }

            await using var ctx1 = new TestDbContext(options);
            await using var ctx2 = new TestDbContext(options);
            await using var uow1 = new EfUnitOfWork<TestDbContext>(ctx1);
            await using var uow2 = new EfUnitOfWork<TestDbContext>(ctx2);

            await uow1.BeginTransactionAsync();
            var entity1 = await ctx1.Entities.FirstAsync();
            entity1.Name = "ctx1";
            await ctx1.SaveChangesAsync();

            var action = async () =>
            {
                await uow2.ExecuteInTransactionAsync(async _ =>
                {
                    var entity2 = await ctx2.Entities.FirstAsync();
                    entity2.Name = "ctx2";
                    await ctx2.SaveChangesAsync();
                });
            };

            await action.Should().ThrowAsync<SqliteException>()
                .WithMessage("*locked*");

            await uow1.CommitAsync();

            await using var verify = new TestDbContext(options);
            var final = await verify.Entities.SingleAsync();
            final.Name.Should().Be("ctx1");

            var txField = typeof(EfUnitOfWork<TestDbContext>)
                .GetField("_currentTransaction", BindingFlags.NonPublic | BindingFlags.Instance);
            txField!.GetValue(uow2).Should().BeNull();
        }

        [Fact]
        public async Task SaveChangesAsync_PersistsWithoutExplicitTransaction()
        {
            var ctx = CreateRelationalContext();
            using var connection = ctx.Connection;
            await using var context = new TestDbContext(ctx.Options);
            await using var unitOfWork = new EfUnitOfWork<TestDbContext>(context);

            context.Entities.Add(new TestEntity { Name = "direct-save" });
            var affected = await unitOfWork.SaveChangesAsync();

            affected.Should().Be(1);

            await using var verificationContext = new TestDbContext(ctx.Options);
            (await verificationContext.Entities.CountAsync()).Should().Be(1);
        }

        [Fact]
        public async Task ExecuteInTransactionAsync_DoesNotStartTransactionForNonRelational()
        {
            var options = new DbContextOptionsBuilder<TestDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            await using var context = new TestDbContext(options);
            await using var unitOfWork = new EfUnitOfWork<TestDbContext>(context);

            await unitOfWork.ExecuteInTransactionAsync(async _ =>
            {
                context.Entities.Add(new TestEntity { Name = "inmemory" });
                await context.SaveChangesAsync();
            });

            context.Database.CurrentTransaction.Should().BeNull();
            (await context.Entities.CountAsync()).Should().Be(1);
        }

        [Fact]
        public async Task Dispose_SyncClearsTransactionAndContext()
        {
            const string connectionString = "DataSource=:memory:";
            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            var options = new DbContextOptionsBuilder<TrackingDbContext>()
                .UseSqlite(connection)
                .Options;

            using (var setup = new TrackingDbContext(options))
            {
                setup.Database.EnsureCreated();
            }

            await using var context = new TrackingDbContext(options);
            var unitOfWork = new EfUnitOfWork<TrackingDbContext>(context);

            await unitOfWork.BeginTransactionAsync();
            var txField = typeof(EfUnitOfWork<TrackingDbContext>)
                .GetField("_currentTransaction", BindingFlags.NonPublic | BindingFlags.Instance);
            txField.Should().NotBeNull();

            ((IDisposable)unitOfWork).Dispose();

            context.WasDisposed.Should().BeTrue();
            txField!.GetValue(unitOfWork).Should().BeNull();
        }

        [Fact]
        public async Task ExecuteInTransactionAsync_ReusesExistingTransactionWithoutCommitting()
        {
            var ctx = CreateRelationalContext();
            using var connection = ctx.Connection;
            await using var context = new TestDbContext(ctx.Options);
            await using var unitOfWork = new EfUnitOfWork<TestDbContext>(context);

            await unitOfWork.BeginTransactionAsync();
            var existingTransaction = context.Database.CurrentTransaction;

            await unitOfWork.ExecuteInTransactionAsync(async _ =>
            {
                context.Entities.Add(new TestEntity { Name = "pending" });
                await context.SaveChangesAsync();
            });

            context.Database.CurrentTransaction.Should().BeSameAs(existingTransaction);

            await unitOfWork.CommitAsync();

            await using var verificationContext = new TestDbContext(ctx.Options);
            var count = await verificationContext.Entities.CountAsync();
            count.Should().Be(1);
        }

        [Fact]
        public async Task ExecuteInTransactionAsync_RollsBackWhenSaveFails()
        {
            const string connectionString = "DataSource=:memory:";
            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            var options = new DbContextOptionsBuilder<ThrowingDbContext>()
                .UseSqlite(connection)
                .Options;

            using (var setup = new ThrowingDbContext(options))
            {
                setup.Database.EnsureCreated();
            }

            await using var context = new ThrowingDbContext(options);
            await using var unitOfWork = new EfUnitOfWork<ThrowingDbContext>(context);

            var action = async () => await unitOfWork.ExecuteInTransactionAsync(async _ =>
            {
                context.Entities.Add(new TestEntity { Name = "will-fail" });
                await context.SaveChangesAsync();
            });

            await action.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("save failed");

            var txField = typeof(EfUnitOfWork<ThrowingDbContext>)
                .GetField("_currentTransaction", BindingFlags.NonPublic | BindingFlags.Instance);
            txField!.GetValue(unitOfWork).Should().BeNull();

            await using var verification = new ThrowingDbContext(options);
            (await verification.Entities.CountAsync()).Should().Be(0);
        }

        [Fact]
        public async Task BeginTransactionAsync_ThrowsWhenCancelled()
        {
            var ctx = CreateRelationalContext();
            using var connection = ctx.Connection;
            await using var context = new TestDbContext(ctx.Options);
            await using var unitOfWork = new EfUnitOfWork<TestDbContext>(context);
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            var action = () => unitOfWork.BeginTransactionAsync(cts.Token);

            await action.Should().ThrowAsync<OperationCanceledException>();
        }

        [Fact]
        public async Task ExecuteInTransactionAsync_ThrowsWhenCancelled()
        {
            var ctx = CreateRelationalContext();
            using var connection = ctx.Connection;
            await using var context = new TestDbContext(ctx.Options);
            await using var unitOfWork = new EfUnitOfWork<TestDbContext>(context);
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            var action = () => unitOfWork.ExecuteInTransactionAsync(_ => Task.CompletedTask, cts.Token);

            await action.Should().ThrowAsync<OperationCanceledException>();
        }

        [Fact]
        public async Task DisposeAsync_ClearsTransactionAndDisposesContext()
        {
            const string connectionString = "DataSource=:memory:";
            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            var options = new DbContextOptionsBuilder<TrackingDbContext>()
                .UseSqlite(connection)
                .Options;

            using (var setup = new TrackingDbContext(options))
            {
                setup.Database.EnsureCreated();
            }

            await using var context = new TrackingDbContext(options);
            await using var unitOfWork = new EfUnitOfWork<TrackingDbContext>(context);

            await unitOfWork.BeginTransactionAsync();

            var txField = typeof(EfUnitOfWork<TrackingDbContext>)
                .GetField("_currentTransaction", BindingFlags.NonPublic | BindingFlags.Instance);
            txField.Should().NotBeNull();
            txField!.GetValue(unitOfWork).Should().NotBeNull();

            await unitOfWork.DisposeAsync();

            context.WasDisposed.Should().BeTrue();
            txField.GetValue(unitOfWork).Should().BeNull();
        }

        [Fact]
        public async Task ExecuteInTransactionAsync_AllowsConcurrentUnitsOverSharedDatabase()
        {
            const string connectionString = "Data Source=concurrency;Mode=Memory;Cache=Shared";
            using var keeperConnection = new SqliteConnection(connectionString);
            keeperConnection.Open();

            var options = new DbContextOptionsBuilder<TestDbContext>()
                .UseSqlite(connectionString)
                .Options;

            // 初始化 schema
            using (var setup = new TestDbContext(options))
            {
                setup.Database.EnsureCreated();
            }

            var tasks = Enumerable.Range(0, 20).Select(async i =>
            {
                await using var context = new TestDbContext(options);
                await using var unitOfWork = new EfUnitOfWork<TestDbContext>(context);

                await unitOfWork.ExecuteInTransactionAsync(async _ =>
                {
                    context.Entities.Add(new TestEntity { Name = $"item-{i}" });
                    await context.SaveChangesAsync();
                });
            }).ToArray();

            await Task.WhenAll(tasks);

            await using var verification = new TestDbContext(options);
            var count = await verification.Entities.CountAsync();

            count.Should().Be(tasks.Length);
        }

        private static RelationalTestContext CreateRelationalContext()
        {
            var connection = new SqliteConnection("DataSource=:memory:");
            connection.Open();

            var options = new DbContextOptionsBuilder<TestDbContext>()
                .UseSqlite(connection)
                .Options;

            using (var context = new TestDbContext(options))
            {
                context.Database.EnsureCreated();
            }

            return new RelationalTestContext(connection, options);
        }

        private sealed record RelationalTestContext(SqliteConnection Connection, DbContextOptions<TestDbContext> Options);

        private sealed class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options)
        {
            public DbSet<TestEntity> Entities => Set<TestEntity>();
        }

        private sealed class TrackingDbContext(DbContextOptions<TrackingDbContext> options) : DbContext(options)
        {
            public bool WasDisposed { get; private set; }

            public DbSet<TestEntity> Entities => Set<TestEntity>();

            public override void Dispose()
            {
                WasDisposed = true;
                base.Dispose();
            }

            public override ValueTask DisposeAsync()
            {
                WasDisposed = true;
                return base.DisposeAsync();
            }
        }

        private sealed class ThrowingDbContext(DbContextOptions<ThrowingDbContext> options) : DbContext(options)
        {
            public DbSet<TestEntity> Entities => Set<TestEntity>();

            public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
                => throw new InvalidOperationException("save failed");
        }

        private sealed class TestEntity
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
        }
    }
}
