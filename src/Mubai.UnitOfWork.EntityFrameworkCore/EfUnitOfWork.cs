using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Mubai.UnitOfWork.EntityFrameworkCore
{
    public class EfUnitOfWork<TContext>(TContext dbContext) : IUnitOfWork<TContext> where TContext : DbContext
    {
        private readonly TContext _dbContext = dbContext;

        private IDbContextTransaction? _currentTransaction;

        public TContext DbContext => _dbContext;
        public async Task<bool> BeginTransactionAsync(CancellationToken token = default)
        {
            // Non-relational databases don't need transactions, directly return false
            // If there's already a transaction, it means it's a "nested call", reuse the existing transaction
            if (_currentTransaction is not null || !_dbContext.Database.IsRelational())
            {
                return false;
            }

            _currentTransaction = await _dbContext.Database.BeginTransactionAsync(token);
            return true;
        }

        public async Task CommitAsync(CancellationToken token = default)
        {
            if (_currentTransaction is null)
            {
                // No explicit transaction, do nothing
                return;
            }

            await _currentTransaction.CommitAsync(token);
            await _currentTransaction.DisposeAsync();
            _currentTransaction = null;
        }

        public async Task RollbackAsync()
        {
            if (_currentTransaction is null)
            {
                return;
            }

            await _currentTransaction.RollbackAsync();
            await _currentTransaction.DisposeAsync();
            _currentTransaction = null;
        }

        public Task<int> SaveChangesAsync(CancellationToken token = default)
            => _dbContext.SaveChangesAsync(token);

        public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, CancellationToken token = default)
        {
            // If there's no current transaction, open one; if there is, reuse it and skip commit/rollback here
            var transactionStarted = await BeginTransactionAsync(token);

            try
            {
                // Here in the delegate you decide when to call SaveChangesAsync
                await operation(token);
                if (transactionStarted)
                {
                    await CommitAsync(token);
                }
            }
            catch
            {
                if (transactionStarted)
                {
                    await RollbackAsync();
                }

                throw;
            }
        }

        public async ValueTask DisposeAsync()
        {
            // Asynchronous disposal
            if (_currentTransaction is IAsyncDisposable asyncTx)
            {
                await asyncTx.DisposeAsync();
            }
            else
            {
                _currentTransaction?.Dispose();
            }
            _currentTransaction = null;

            if (_dbContext is IAsyncDisposable asyncDb)
            {
                await asyncDb.DisposeAsync();
            }
            else
            {
                _dbContext.Dispose();
            }

            GC.SuppressFinalize(this);
        }

        void IDisposable.Dispose()
        {
            // Synchronously call asynchronous disposal (to prevent someone from using using instead of await using)
            DisposeAsync().AsTask().GetAwaiter().GetResult();
            GC.SuppressFinalize(this);
        }
    }
}

