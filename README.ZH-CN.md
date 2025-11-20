# Mubai.UnitOfWork

[English](./README.md) | [简体中文]

一个轻量的工作单元抽象及其 EF Core 实现。Mubai.UnitOfWork.EntityFrameworkCore 已可用，后续会补充 Dapper 等其他 ORM。

## Packages

- Mubai.UnitOfWork：最小化的 IUnitOfWork 接口（事务 + 异步释放），目标框架 netstandard2.0。
- Mubai.UnitOfWork.EntityFrameworkCore：封装 DbContext 的 EF Core 10+ 实现，目标框架 net10.0。

## Features

- 简单接口：BeginTransactionAsync / CommitAsync / RollbackAsync / SaveChangesAsync / - - ExecuteInTransactionAsync。
- 事务复用：检测已有事务，避免重复或嵌套开启。
- 非关系型安全：对 InMemory 等提供程序不会开启事务。
- 全异步，支持 IDisposable/IAsyncDisposable。
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
   services.AddScoped<IEfUnitOfWork<AppDbContext>, EfUnitOfWork<AppDbContext>>();
   ```

3. 自动事务包裹
   
   ```csharp
    await unitOfWork.ExecuteInTransactionAsync(async ct =>
    {
        db.Set<Post>().Add(new Post { Title = "Hello" });
        await db.SaveChangesAsync(ct);
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

## 路线图

- 其他主流 ORM 适配
- 更多示例与模板