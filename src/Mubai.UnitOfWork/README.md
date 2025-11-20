# Mubai.UnitOfWork
English | [简体中文](./README.ZH-CN.md)

## About
Interfaces for a simple Unit of Work pattern. It exposes the minimal contract `IUnitOfWork` for transactional workflows; you can provide your own data-provider implementation. For EF Core usage, see the separate `Mubai.UnitOfWork.EntityFrameworkCore` package.

## How to Use
Reference this project/package and register your own implementation of `IUnitOfWork`.

### Automated Execution
Use `ExecuteInTransactionAsync` to complete the transaction by wrapping business operations.
   ```csharp
   await unitOfWork.ExecuteInTransactionAsync(async ct =>
   {
       // perform data changes
       await unitOfWork.SaveChangesAsync(ct);
   });
   ```
### Manual control
Manually control boundaries with `BeginTransactionAsync` / `CommitAsync` / `RollbackAsync`.
```csharp
if (await unitOfWork.BeginTransactionAsync())
{
    try
    {
        // business work
        await unitOfWork.SaveChangesAsync();
        await unitOfWork.CommitAsync();
    }
    catch
    {
        await unitOfWork.RollbackAsync();
        throw;
    }
}
```

## Requirements
- .NET Standard 2.0