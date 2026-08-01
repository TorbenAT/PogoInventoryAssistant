using Microsoft.Data.Sqlite;
using PogoInventory.Persistence;

namespace PogoInventory.SelfTest;

internal static class PersistentWorkLedgerTests
{
    public static async Task RunAsync()
    {
        var root = Path.Combine(Path.GetTempPath(), "pogo-work-ledger-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var database = Path.Combine(root, "ledger.sqlite");
        try
        {
            var ledger = new InventoryPersistenceService(database);
            await ledger.UpsertWorkBucketAsync(Bucket("2016-07", new DateOnly(2016, 7, 1)));
            await ledger.UpsertWorkBucketAsync(Bucket("2016-08", new DateOnly(2016, 8, 1)));
            await ledger.UpsertWorkBucketAsync(Bucket("2017", new DateOnly(2017, 1, 1)));
            AssertEqual("2016-07", (await ledger.LoadOldestUnfinishedWorkBucketAsync())?.LogicalBucketId, "oldest planned bucket");

            await ledger.RecordWorkItemAsync(new PersistentWorkItem
            {
                LogicalBucketId = "2016-07", LocalPokemonId = "run-a:000001", State = PersistentWorkItemState.TagPending, Disposition = "REVIEW", ExactBindingEvidence = "exact-test-binding", UpdatedAtUtc = DateTimeOffset.UtcNow
            }, new PersistentWorkAttempt
            {
                LogicalBucketId = "2016-07", LocalPokemonId = "run-a:000001", AttemptKind = "PersistBeforeTag", Result = "Durable", OccurredAtUtc = DateTimeOffset.UtcNow
            });
            await ledger.SetWorkBucketStatusAsync("2016-07", PersistentWorkBucketStatus.Complete, "{\"emptyQueryVerified\":true,\"reconciled\":true}");

            var reopened = new InventoryPersistenceService(database);
            AssertEqual("2016-08", (await reopened.LoadOldestUnfinishedWorkBucketAsync())?.LogicalBucketId, "completed bucket is not revisited after reopen");
            await using var connection = new SqliteConnection($"Data Source={database}");
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM WorkAttempts WHERE LogicalBucketId='2016-07' AND LocalPokemonId='run-a:000001';";
            AssertEqual(1L, Convert.ToInt64(await command.ExecuteScalarAsync()), "append-only pre-tag attempt persisted");
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(root, recursive: true);
        }
    }

    private static PersistentWorkBucket Bucket(string id, DateOnly start) => new()
    {
        LogicalBucketId = id, AbsoluteDateStart = start, AbsoluteDateEnd = start.AddMonths(1).AddDays(-1), DerivedPhoneQuery = "year" + start.Year
    };

    private static void AssertEqual<T>(T expected, T actual, string label)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new InvalidOperationException($"Expected {label} to be '{expected}', got '{actual}'.");
    }
}
