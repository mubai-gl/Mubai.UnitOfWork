# Mubai.UnitOfWork

[English](./README.md) | 简体中文

一个轻量的工作单元抽象及其 EF Core 实现。`Mubai.UnitOfWork.EntityFrameworkCore` 已可用，后续会补充 Dapper 等 ORM。

## Packages

- `Mubai.UnitOfWork`：最小化的 `IUnitOfWork` 抽象（事务 + 异步释放），目标框架 `netstandard2.0`。
- `Mubai.UnitOfWork.EntityFrameworkCore`：封装 `DbContext` 的 EF Core 10+ 实现，目标框架 `net10.0`。

## Features

- 简洁接口：`BeginTransactionAsync`、`CommitAsync`、`RollbackAsync`、`SaveChangesAsync`、`ExecuteInTransactionAsync`。
- 事务复用：检测已有事务，避免重复或嵌套开启。
- 非关系型安全：对 InMemory 等提供程序不会开启事务。
- 全异步，支持 `IDisposable` / `IAsyncDisposable`。
- xUnit 覆盖取消、复用、并发、回滚等场景。

## 快速开始（EF Core）

1. 安装
   ```bash
   dotnet add package Mubai.UnitOfWork
   dotnet add package Mubai.UnitOfWork.EntityFrameworkCore
   ```

2. DI 注册
   ```csharp
   services.AddDbContext<AppDbContext>(...);

   // 如果只用一个 DbContext
   services.AddScoped<IUnitOfWork<AppDbContext>, EfUnitOfWork<AppDbContext>>();
   // 若需要通过非泛型接口解析，额外转接一层
   services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<IUnitOfWork<AppDbContext>>());
   ```

3. 自动事务包裹
   ```csharp
   await unitOfWork.ExecuteInTransactionAsync(async ct =>
   {
       db.Set<Post>().Add(new Post { Title = "Hello" });
       await unitOfWork.SaveChangesAsync(ct);
   });
   ```

4. 手动事务控制
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

## 多个 DbContext 注册示例（避免覆盖）

```csharp
services.AddDbContext<MainDbContext>(...);
services.AddDbContext<AuditDbContext>(...);

// 开放泛型注册一次，按需注入具体的 IUnitOfWork<TContext>
services.AddScoped(typeof(IUnitOfWork<>), typeof(EfUnitOfWork<>));

public class FooService(
    IUnitOfWork<MainDbContext> mainUow,
    IUnitOfWork<AuditDbContext> auditUow)
{
    // 分别控制不同 DbContext 的事务
}
```

> 不要用同一个非泛型接口重复注册多个实现，例如：
> ```csharp
> services.AddScoped<IUnitOfWork, EfUnitOfWork<MainDbContext>>();
> services.AddScoped<IUnitOfWork, EfUnitOfWork<AuditDbContext>>();
> ```
> 上面写法的第二行会覆盖第一行。

## 路线图

- 其他主流 ORM 适配
- 更多示例与模板
