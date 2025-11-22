# Mubai.UnitOfWork

English | [简体中文](./README.ZH-CN.md)

A lightweight Unit of Work abstraction plus an EF Core implementation. `Mubai.UnitOfWork.EntityFrameworkCore` ships today; Dapper and other ORMs will be added next.

## Packages

- `Mubai.UnitOfWork` — minimal `IUnitOfWork` abstraction (transactions + async disposal) targeting `netstandard2.0`.
- `Mubai.UnitOfWork.EntityFrameworkCore` — EF Core 10+ implementation wrapping `DbContext` with async transactions, `net10.0`.

## Features

- Small surface: `BeginTransactionAsync`, `CommitAsync`, `RollbackAsync`, `SaveChangesAsync`, `ExecuteInTransactionAsync`.
- Safe transaction reuse: detects existing transactions and avoids nested creation.
- Works with non-relational providers: skips transaction start for InMemory, etc.
- Async-first and IDisposable/IAsyncDisposable support.
- Covered by xUnit tests (cancellation, reuse, concurrency, rollback on failure).

## Quickstart (EF Core)

1. Install
   
   ```bash
   dotnet add package Mubai.UnitOfWork
   dotnet add package Mubai.UnitOfWork.EntityFrameworkCore
   ```

2. Register in DI
   
   ```csharp
   services.AddDbContext<AppDbContext>(...);

   // If you only have one DbContext
   services.AddScoped<IUnitOfWork<AppDbContext>, EfUnitOfWork<AppDbContext>>();
   // If you still resolve via the non-generic interface, forward it
   services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<IUnitOfWork<AppDbContext>>());
   ```

3. Automatic transaction boundary
   
   ```csharp
    await unitOfWork.ExecuteInTransactionAsync(async ct =>
    {
        db.Set<Post>().Add(new Post { Title = "Hello" });
        await unitOfWork.SaveChangesAsync(ct);
    });
   ```

4. Manual control when you need explicit boundaries
   
   ```csharp
    if (await unitOfWork.BeginTransactionAsync())
    {
        try
        {
            db.Set<Post>().Add(new Post { Title = "Hello" });
            await db.SaveChangesAsync();
            await unitOfWork.CommitAsync();
        }
        catch
        {
            await unitOfWork.RollbackAsync();
            throw;
        }
    }
   ```

## Multiple DbContexts (avoid overriding)

```csharp
services.AddDbContext<MainDbContext>(...);
services.AddDbContext<AuditDbContext>(...);

// Register the open generic once; inject the concrete IUnitOfWork<TContext> where needed
services.AddScoped(typeof(IUnitOfWork<>), typeof(EfUnitOfWork<>));

public class FooService(
    IUnitOfWork<MainDbContext> mainUow,
    IUnitOfWork<AuditDbContext> auditUow)
{
    // Control transactions for different DbContexts separately
}
```

> Avoid registering multiple implementations on the same non-generic interface, e.g.:
> ```csharp
> services.AddScoped<IUnitOfWork, EfUnitOfWork<MainDbContext>>();
> services.AddScoped<IUnitOfWork, EfUnitOfWork<AuditDbContext>>();
> ```
> The second line will override the first.

## Roadmap

- Other popular ORMs (Dapper, etc.) following the same IUnitOfWork contract
- Additional samples and templates
