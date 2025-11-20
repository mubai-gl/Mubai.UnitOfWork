using System;
using System.Threading;
using System.Threading.Tasks;

namespace Mubai.UnitOfWork.Abstractions
{
    public interface IUnitOfWork
    {
        Task<bool> BeginTransactionAsync(CancellationToken token = default);
        Task CommitAsync(CancellationToken token = default);
        Task RollbackAsync();
        Task<int> SaveChangesAsync(CancellationToken token = default);
        Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, CancellationToken token = default);
    }
}
