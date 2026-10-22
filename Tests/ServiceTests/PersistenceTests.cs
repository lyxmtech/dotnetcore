using Dapper;
using DataCore.Audit;
using DataCore.Requests;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ServiceTests;

public class PersistenceTests
{
    [Fact]
    public async Task RequestsRepository_Insert_Then_ReadBack()
    {
        using var db = new TempDatabase();
        await db.InitializeAsync(RequestsSchema.Statements);

        var repository = new RequestsRepository(db.CreateFactory(), NullLogger<RequestsRepository>.Instance);

        var at = new DateTimeOffset(2026, 10, 21, 12, 30, 0, TimeSpan.Zero);
        var id = await repository.InsertAsync("Alice", at);

        Assert.True(id > 0);

        var rows = await repository.GetRecentAsync();

        var row = Assert.Single(rows);
        Assert.Equal(id, row.Id);
        Assert.Equal("Alice", row.Name);
        Assert.Equal(at, row.RequestedAt);
    }

    [Fact]
    public async Task RequestsRepository_MultipleRows_AreOrderedNewestFirst()
    {
        using var db = new TempDatabase();
        await db.InitializeAsync(RequestsSchema.Statements);

        var repository = new RequestsRepository(db.CreateFactory(), NullLogger<RequestsRepository>.Instance);
        var now = DateTimeOffset.UtcNow;

        await repository.InsertAsync("first", now);
        await repository.InsertAsync("second", now.AddMinutes(1));
        await repository.InsertAsync("third", now.AddMinutes(2));

        var rows = await repository.GetRecentAsync();

        Assert.Equal(3, rows.Count);
        Assert.Equal("third", rows[0].Name);
        Assert.Equal("first", rows[2].Name);
    }

    [Fact]
    public async Task AuditRepository_RecordAsync_StoresRowAndBumpsCounter()
    {
        using var db = new TempDatabase();
        await db.InitializeAsync(AuditSchema.Statements);

        var repository = new AuditRepository(db.CreateFactory(), NullLogger<AuditRepository>.Instance);

        var requestedAt = new DateTimeOffset(2026, 10, 21, 9, 0, 0, TimeSpan.Zero);
        var recordedAt = new DateTimeOffset(2026, 10, 21, 9, 0, 1, TimeSpan.Zero);

        var id = await repository.RecordAsync("Bob", requestedAt, recordedAt);

        Assert.True(id > 0);

        var rows = await repository.GetRecentAsync();
        var row = Assert.Single(rows);
        Assert.Equal("Bob", row.Name);
        Assert.Equal(requestedAt, row.RequestedAt);
        Assert.Equal(recordedAt, row.RecordedAt);

        // The counter is updated in the same transaction as the row.
        var stats = await repository.GetStatsAsync();
        Assert.Equal(1, stats.Total);
    }

    [Fact]
    public async Task AuditRepository_Counter_StaysInSyncWithRows()
    {
        using var db = new TempDatabase();
        await db.InitializeAsync(AuditSchema.Statements);

        var repository = new AuditRepository(db.CreateFactory(), NullLogger<AuditRepository>.Instance);
        var now = DateTimeOffset.UtcNow;

        for (var i = 0; i < 5; i++)
            await repository.RecordAsync($"user-{i}", now, now);

        var rows = await repository.GetRecentAsync();
        var stats = await repository.GetStatsAsync();

        Assert.Equal(5, rows.Count);
        Assert.Equal(rows.Count, stats.Total);
    }

    [Fact]
    public async Task TwoServices_UseSeparateDatabases_EachSeesOnlyItsOwnTable()
    {
        // The "database per service" rule: the requests table lives in one file,
        // the audit table in another, and neither can see the other's rows.
        using var helloDb = new TempDatabase();
        using var auditDb = new TempDatabase();

        await helloDb.InitializeAsync(RequestsSchema.Statements);
        await auditDb.InitializeAsync(AuditSchema.Statements);

        var requests = new RequestsRepository(helloDb.CreateFactory(), NullLogger<RequestsRepository>.Instance);
        var audit = new AuditRepository(auditDb.CreateFactory(), NullLogger<AuditRepository>.Instance);

        await requests.InsertAsync("Alice", DateTimeOffset.UtcNow);
        await audit.RecordAsync("Alice", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

        Assert.Single(await requests.GetRecentAsync());
        Assert.Single(await audit.GetRecentAsync());

        // The requests table does not exist in the audit database at all.
        await using var connection = await auditDb.CreateFactory().CreateAsync();
        var tableExists = await connection.ExecuteScalarAsync<long>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'requests';");

        Assert.Equal(0, tableExists);
    }

    [Fact]
    public async Task InitializeAsync_IsIdempotent_SecondRunDoesNotFail()
    {
        using var db = new TempDatabase();

        await db.InitializeAsync(RequestsSchema.Statements);
        await db.InitializeAsync(RequestsSchema.Statements);

        var repository = new RequestsRepository(db.CreateFactory(), NullLogger<RequestsRepository>.Instance);
        await repository.InsertAsync("Alice", DateTimeOffset.UtcNow);

        Assert.Single(await repository.GetRecentAsync());
    }
}
