using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace DbCore;

/// <summary>
/// Shared SQLite wiring for every service that owns a database.
/// Mirrors the BusCore pattern: infrastructure only, no business logic.
/// </summary>
public static class DatabaseSetup
{
    /// <summary>
    /// Registers the SQLite connection factory and runs the given DDL once at startup.
    /// The schema is owned by the calling service, which keeps the
    /// "database per service" rule: no service can reach another service's tables.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Application configuration ("Database" section).</param>
    /// <param name="schemaStatements">Idempotent DDL statements for this service.</param>
    public static IServiceCollection AddSqliteDatabase(
        this IServiceCollection services,
        IConfiguration configuration,
        params string[] schemaStatements)
    {
        services.Configure<DatabaseOptions>(configuration.GetSection(DatabaseOptions.SectionName));
        services.AddSingleton<IDbConnectionFactory, SqliteConnectionFactory>();
        services.AddSingleton<DatabaseInitializer>();

        services.AddHostedService<DatabaseInitializationService>(sp => new DatabaseInitializationService(
            sp.GetRequiredService<DatabaseInitializer>(),
            schemaStatements));

        return services;
    }

    /// <summary>
    /// Convenience overload for services that already have an options instance.
    /// </summary>
    public static IServiceCollection AddSqliteDatabase(
        this IServiceCollection services,
        DatabaseOptions options,
        params string[] schemaStatements)
    {
        services.AddSingleton<IOptions<DatabaseOptions>>(Options.Create(options));
        services.AddSingleton<IDbConnectionFactory, SqliteConnectionFactory>();
        services.AddSingleton<DatabaseInitializer>();

        services.AddHostedService<DatabaseInitializationService>(sp => new DatabaseInitializationService(
            sp.GetRequiredService<DatabaseInitializer>(),
            schemaStatements));

        return services;
    }
}

/// <summary>
/// Runs <see cref="DatabaseInitializer"/> during application startup.
/// </summary>
public class DatabaseInitializationService : IHostedService
{
    private readonly DatabaseInitializer _initializer;
    private readonly string[] _schemaStatements;

    public DatabaseInitializationService(DatabaseInitializer initializer, string[] schemaStatements)
    {
        _initializer = initializer;
        _schemaStatements = schemaStatements;
    }

    public Task StartAsync(CancellationToken cancellationToken) => _initializer.InitializeAsync(_schemaStatements, cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
