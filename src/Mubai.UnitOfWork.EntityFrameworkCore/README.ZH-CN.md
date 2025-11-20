# Mubai.UnitOfWork
English(./README.md) | [简体中文]

## 关于
EF Core 的工作单元实现。封装 `DbContext`，提供事务开启、提交、回滚与异步释放的统一入口。

## 如何使用
引用本项目/包，并在容器中注册你实现的 `IUnitOfWork`。

## DI 注册
引用项目或包，并通过 DI 注册
   ```csharp
   services.AddDbContext<AppDbContext>(...);
   services.AddScoped<IEfUnitOfWork<AppDbContext>, EfUnitOfWork<AppDbContext>>();
   ```
### 自动执行
使用`ExecuteInTransactionAsync`通过包裹业务，完成对事务的控制
   ```csharp
   await unitOfWork.ExecuteInTransactionAsync(async ct =>
   {
       // 执行业务修改
       db.Set<Entity>().Add(item);
       await unitOfWork.SaveChangesAsync(ct);
   });
   ```

### 手动控制
使用 `BeginTransactionAsync` / `CommitAsync` / `RollbackAsync`。
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