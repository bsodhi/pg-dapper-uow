// -----------------------------------------------------------------------------
// PostgresDapperUow - PostgreSQL transactional repository infrastructure.
// Licensed under the MIT License.
// -----------------------------------------------------------------------------

using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using PostgresDapperUow.DependencyInjection;
using PostgresDapperUow.Abstractions;
using PostgresDapperUow.Attributes;
using PostgresDapperUow.Exceptions;

public partial class Program
{
    private static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        builder.Services.AddPostgresDapper(
            builder.Configuration.GetConnectionString("Default")!);

        var app = builder.Build();

        app.UseSwagger();
        app.UseSwaggerUI();

        app.MapGet("/", () => Results.Ok("PostgresDapperUow Minimal API Sample"));

        /*
        |--------------------------------------------------------------------------
        | Users API
        |--------------------------------------------------------------------------
        */

        app.MapPost("/users", async (
            CreateUserRequest request,
            IRepository<User> repo,
            IUnitOfWork uow) =>
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                return Results.BadRequest("Name is required.");

            if (string.IsNullOrWhiteSpace(request.Email))
                return Results.BadRequest("Email is required.");

            var user = new User
            {
                Name = request.Name,
                Email = request.Email,
                Metadata = request.Metadata is null
                    ? null
                    : JsonSerializer.Serialize(request.Metadata)
            };

            var id = await repo.Create(user);
            uow.Commit();

            return Results.Created($"/users/{id}", new { id });
        });

        app.MapGet("/users/{id:long}", async (
            long id,
            IRepository<User> repo) =>
        {
            var user = await repo.GetById(id);

            if (user is null)
                return Results.NotFound();

            return Results.Ok(ToResponse(user));
        });

        app.MapGet("/users", async (
            [FromQuery] string? name,
            IRepository<User> repo) =>
        {
            var users = name is null
                ? await repo.FindMany()
                : await repo.FindMany(new { Name = name });

            return Results.Ok(users.Select(ToResponse));
        });

        app.MapPut("/users/{id:long}", async (
            long id,
            UpdateUserRequest request,
            IRepository<User> repo,
            IUnitOfWork uow) =>
        {
            var existing = await repo.GetById(id);

            if (existing is null)
                return Results.NotFound();

            existing.Name = request.Name ?? existing.Name;
            existing.Email = request.Email ?? existing.Email;

            if (request.Metadata is not null)
                existing.Metadata = JsonSerializer.Serialize(request.Metadata);

            try
            {
                await repo.Update(existing);
                uow.Commit();
            }
            catch (ConcurrencyException)
            {
                return Results.Conflict("The record was modified by another process.");
            }

            return Results.Ok(ToResponse(existing));
        });

        app.MapDelete("/users/{id:long}", async (
            long id,
            IRepository<User> repo,
            IUnitOfWork uow) =>
        {
            var deleted = await repo.DeleteById(id);

            if (!deleted)
                return Results.NotFound();

            uow.Commit();
            return Results.NoContent();
        });

        app.Run();

        /*
        |--------------------------------------------------------------------------
        | Helpers
        |--------------------------------------------------------------------------
        */

        static UserResponse ToResponse(User user)
        {
            return new UserResponse(
                user.Id,
                user.Name,
                user.Email,
                user.Metadata is null
                    ? null
                    : JsonSerializer.Deserialize<Dictionary<string, string>>(user.Metadata),
                user.TxnNo
            );
        }
    }
}

/*
|--------------------------------------------------------------------------
| Models
|--------------------------------------------------------------------------

Before running this API, ensure that the following table exists in postgres.

CREATE TABLE users (
    id BIGSERIAL PRIMARY KEY,
    txn_no INT NOT NULL DEFAULT 1,
    inserted_at TIMESTAMP NOT NULL DEFAULT now(),
    updated_at TIMESTAMP NOT NULL DEFAULT now(),
    name TEXT NOT NULL,
    email TEXT NOT NULL,
    metadata JSONB NULL
);
*/

[Table("users")]
public sealed class User : BaseEntity
{
    public string Name { get; set; } = default!;
    public string Email { get; set; } = default!;

    [JsonColumn("metadata")]
    public string? Metadata { get; set; }
}

public sealed record CreateUserRequest(
    string Name,
    string Email,
    Dictionary<string, string>? Metadata
);

public sealed record UpdateUserRequest(
    string? Name,
    string? Email,
    Dictionary<string, string>? Metadata
);

public sealed record UserResponse(
    long Id,
    string Name,
    string Email,
    Dictionary<string, string>? Metadata,
    int TxnNo
);