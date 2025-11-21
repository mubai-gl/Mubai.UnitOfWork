using Microsoft.EntityFrameworkCore;
using Mubai.UnitOfWork.Abstractions;

namespace Mubai.UnitOfWork.EntityFrameworkCore
{
    public interface IEfUnitOfWork<TContext> : IUnitOfWork where TContext : DbContext
    {
        TContext DbContext { get; }
    }
}
