# Mubai.UnitOfWork

## About
A lightweight EF Core Unit of Work helper that manages transactions, commits, rollbacks, and disposal for `DbContext`. Includes an xUnit test suite covering transactional workflows, cancellation, concurrent access, and stability.

## How to Use
1. Install the library (project reference or package) and register your `DbContext`.
2. Wrap data operations:
   ```csharp
   await unitOfWork.ExecuteInTransactionAsync(async ct =>
   {
       db.Set<Entity>().Add(item);
       await db.SaveChangesAsync(ct);
   });
   ```
3. Use `BeginTransactionAsync` / `CommitAsync` / `RollbackAsync` for manual control when you need explicit boundaries.

Manual control example:
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

## Requirements
- .NET 10 (preview SDK)  
- EF Core 10.x

## Testing
- Run: `dotnet test Mubai.UnitOfWork.sln`
- Coverage (local): `dotnet test Mubai.UnitOfWork.sln --collect:"XPlat Code Coverage"`

## CI
GitHub Actions workflow (`.github/workflows/ci.yml`) restores, tests with coverage, and uploads Cobertura results.

---

## 中文版 / Chinese

### About
一个轻量的 EF Core 工作单元辅助库，负责事务开启、提交、回滚与 DbContext 释放。内置 xUnit 测试覆盖事务流程、取消、并发与稳定性。

### How to Use
1. 引用库（项目引用或包）并注册你的 `DbContext`。
2. 包裹数据操作：
   ```csharp
   await unitOfWork.ExecuteInTransactionAsync(async ct =>
   {
       db.Set<Entity>().Add(item);
       await db.SaveChangesAsync(ct);
   });
   ```
3. 如需手动控制，可使用 `BeginTransactionAsync` / `CommitAsync` / `RollbackAsync`。

手动控制示例：
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

### Requirements
- .NET 10（预览 SDK）  
- EF Core 10.x

### Testing
- 执行：`dotnet test Mubai.UnitOfWork.sln`
- 覆盖率（本地）：`dotnet test Mubai.UnitOfWork.sln --collect:"XPlat Code Coverage"`

### CI
GitHub Actions 工作流（`.github/workflows/ci.yml`）负责还原、测试并收集 Cobertura 覆盖率报告。
