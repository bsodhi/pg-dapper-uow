# PostgresDapperUow

> A lightweight, transactional PostgreSQL repository infrastructure built on Dapper.

`PostgresDapperUow` provides a clean and opinionated implementation of:

* Repository pattern
* Unit of Work
* Optimistic concurrency
* Attribute-based mapping
* JSON column support
* PostgreSQL-first design

It is intentionally minimal and does **not** attempt to be a full ORM.

---

## Why This Exists

Many projects want:

* Dapper performance
* Explicit transaction control
* Clean repository abstraction
* Optimistic concurrency built-in
* Zero runtime reflection overhead
* Predictable SQL

This library provides exactly that — nothing more.

It follows YAGNI principles and avoids speculative abstractions.

---

## Features

* ✅ PostgreSQL-first design
* ✅ Transactional `UnitOfWork`
* ✅ Optimistic concurrency (`txn_no`)
* ✅ Attribute-based table/column mapping
* ✅ JSON column serialization
* ✅ Nullable + enum handling
* ✅ Compiled property accessors (no runtime reflection)
* ✅ Deterministic SQL generation
* ✅ CancellationToken support
* ✅ DI integration for ASP.NET Core

---

## Non-Goals

This library intentionally does **not** provide:

* ❌ Generic SQL dialect abstraction
* ❌ Bulk insert engine
* ❌ Projection/query builder DSL
* ❌ LINQ provider
* ❌ Expression tree translation
* ❌ Soft-delete framework
* ❌ Migration tooling

It is infrastructure, not an ORM replacement.

---

## Installation

```bash
dotnet add package PostgresDapperUow
```

---

## Quick Start

### 1. Register Services

```csharp
builder.Services.AddPostgresInfrastructure(
    builder.Configuration.GetConnectionString("Default"));
```

---

### 2. Define an Entity

```csharp
[Table("users")]
public sealed class User : BaseEntity
{
    public string Name { get; set; } = default!;
    public string Email { get; set; } = default!;
}
```

---

### 3. Use in an Application Service

```csharp
public sealed class UserService
{
    private readonly IRepository<User> _repo;
    private readonly IUnitOfWork _uow;

    public UserService(IRepository<User> repo, IUnitOfWork uow)
    {
        _repo = repo;
        _uow = uow;
    }

    public async Task<long> CreateUser(User user)
    {
        var id = await _repo.Create(user);
        _uow.Commit();
        return id;
    }
}
```

---

## Optimistic Concurrency

Entities inheriting from `BaseEntity` automatically use optimistic concurrency:

```sql
UPDATE users
SET ..., txn_no = txn_no + 1
WHERE id = @Id AND txn_no = @TxnNo
```

If a conflict occurs, a `ConcurrencyException` is thrown.

This ensures safe concurrent updates without locks.

---

## Filtering

Supports anonymous object filtering:

```csharp
var users = await repo.FindMany(new
{
    Email = "test@example.com"
});
```

Supports:

* `=`
* `IS NULL`
* `= ANY(@param)` for collections
* Enum filters
* DateOnly filters

---

## JSON Columns

Mark properties with `JsonColumnAttribute`.

Objects will be serialized using `System.Text.Json`.

---

## Architecture Overview

```
Application Layer
       ↓
IRepository<T>
       ↓
DapperRepository<T>
       ↓
IUnitOfWork (Transaction Boundary)
       ↓
Npgsql + PostgreSQL
```

Key principles:

* Repository always operates inside UnitOfWork
* Transaction boundaries are explicit
* No implicit auto-commit
* No hidden behavior

---

## Performance Characteristics

* No runtime reflection (compiled delegates used)
* O(P) parameter binding where P = property count
* Minimal allocations
* Network/database latency dominates execution time

Designed for:

* Web APIs
* Microservices
* Background workers
* Moderate to high throughput systems

---

## Project Structure

```
src/PostgresDapperUow/
tests/PostgresDapperUow.Tests/
samples/MinimalApiSample/
```

---

## Requirements

* .NET 10+
* PostgreSQL 12+
* Dapper
* Npgsql

---

## Testing

Integration tests use PostgreSQL (recommended via Testcontainers or Docker).

Run:

```bash
dotnet test
```

---

## Versioning

This project follows Semantic Versioning:

* MAJOR — breaking changes
* MINOR — new features
* PATCH — bug fixes

---

## Contributing

Contributions are welcome.

Before submitting a PR:

1. Ensure tests pass
2. Add tests for new behavior
3. Avoid speculative abstractions
4. Keep public API surface minimal

See `CONTRIBUTING.md` for details.

---

## License

MIT License.

---

## When To Use This

Use this library if:

* You prefer Dapper over full ORM
* You want explicit transaction boundaries
* You want built-in optimistic concurrency
* You want predictable SQL
* You want a thin, clean infrastructure layer

Do **not** use this if:

* You need LINQ provider
* You need cross-database abstraction
* You need heavy dynamic query generation

---

## Philosophy

This project embraces:

* Explicitness over magic
* Determinism over cleverness
* Minimalism over feature creep
* PostgreSQL-first optimization

