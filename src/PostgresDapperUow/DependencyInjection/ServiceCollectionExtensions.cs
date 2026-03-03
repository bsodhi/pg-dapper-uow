// -----------------------------------------------------------------------------
// PostgresDapperUow - PostgreSQL transactional repository infrastructure.
// Licensed under the MIT License.
// -----------------------------------------------------------------------------

using Dapper;
using Microsoft.Extensions.DependencyInjection;
using PostgresDapperUow.Abstractions;
using PostgresDapperUow.Repository;
using PostgresDapperUow.UnitOfWork;

namespace PostgresDapperUow.DependencyInjection;

/// <summary>
/// Allows you add key abstractions offered by this library as 
/// dependencies to an ASP.NET Core APIs.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="services"></param>
    /// <param name="connectionString"></param>
    /// <returns></returns>
    public static IServiceCollection AddPostgresDapper(
        this IServiceCollection services,
        string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddSingleton<IDbConnectionFactory>(
            new NpgsqlConnectionFactory(connectionString));

        services.AddScoped<IUnitOfWork, DapperUnitOfWork>();
        services.AddScoped(typeof(IRepository<>), typeof(DapperRepository<>));

        return services;
    }
}