# Mubai.UnitOfWork
English(./README.md) | [简体中文]

## 关于
提供工作单元模式的最小接口（`IUnitOfWork`），用于事务处理和异步释放，可在不同数据提供程序中自行实现。如需 EF Core 用法，请查看独立的 `Mubai.UnitOfWork.EntityFrameworkCore` 包。

## 如何使用
引用本项目/包，并在容器中注册你实现的 `IUnitOfWork`。

### 自动执行
使用`ExecuteInTransactionAsync`通过包裹业务，完成对事务的控制
   ```csharp
   await unitOfWork.ExecuteInTransactionAsync(async ct =>
   {
       // 执行业务修改
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
        // 业务处理
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
