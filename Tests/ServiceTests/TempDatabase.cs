using DbCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ServiceTests;

/// <summary>
/// Creates a throw-away SQLite database (one file per test) and wires up the
/// same DbCore services the real services use.
/// </summary>
public sealed class TempDatabase : IDisposable
{
    private readonly string _directory;

    public TempDatabase()
    {
        _directory = Path.Combine(Path.GetTempPath(), "dotnetcore-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
        Path_ = Path.Combine(_directory, "test.db");
    }

    /// <summary>Full path of the temporary SQLite file.</summary>
    public string Path_ { get; }

    public IDbConnectionFactory CreateFactory()
    {
        var options = Options.Create(new DatabaseOptions
        {
            Path = Path_,
            BusyTimeoutSeconds = 5
        });

        return new SqliteConnectionFactory(options);
    }

    /// <summary>Runs the DDL exactly like the services do at startup.</summary>
    public async Task InitializeAsync(params string[] schemaStatements)
    {
        var initializer = new DatabaseInitializer(CreateFactory(), NullLogger<DatabaseInitializer>.Instance);
        await initializer.InitializeAsync(schemaStatements);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_directory))
                Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
            // best effort cleanup
        }
    }
}
