# Mubai.UnitOfWork.EntityFrameworkCore
English | [简体中文](./README.ZH-CN.md)

## About
Entity Framework Core implementation of the Unit of Work abstractions. Wraps a `DbContext` to manage transactions, commits, rollbacks, and disposal with async support.

## How to Use
Reference the project or package alongside your `DbContext`.

## DI
Register via DI
   ```csharp
   services.AddDbContext<AppDbContext>(...);
   services.AddScoped<IUnitOfWork, IUnitOfWork<AppDbContext>>();
   ```

### Automated Execution
Use `ExecuteInTransactionAsync` to complete the transaction by wrapping business operations.
   ```csharp
   await unitOfWork.ExecuteInTransactionAsync(async ct =>
   {
       db.Set<Entity>().Add(item);
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
           db.Set<Entity>().Add(item1);
           await db.SaveChangesAsync();

           db.Set<Entity>().Add(item2);
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
