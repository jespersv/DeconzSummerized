using DeconzSummarized.Summarize;

namespace DeconzSummarized.Tests;

[TestFixture]
public class SummaryStoreTests
{
    private static DailySummary Day(string date, string room) =>
        new() { Date = date, Room = room, Temperature = new MetricStat { Min = 1, Avg = 2, Max = 3, Count = 1 } };

    [Test]
    public void Merge_ReplacesReprocessedDates_AndKeepsOthers()
    {
        var existing = new SummaryDocument
        {
            Days =
            [
                Day("2026-06-16", "Office 1"),
                Day("2026-06-17", "Office 1"), // will be replaced
            ],
        };

        var recomputed = new[] { Day("2026-06-17", "Office 1"), Day("2026-06-17", "Shed") };
        var reprocessed = new HashSet<string> { "2026-06-17" };

        var store = new SummaryStore(Path.GetTempPath());
        var merged = store.Merge(existing, recomputed, reprocessed);

        Assert.That(merged.Days.Count(d => d.Date == "2026-06-17"), Is.EqualTo(2));
        Assert.That(merged.Days.Count(d => d.Date == "2026-06-16"), Is.EqualTo(1));
        Assert.That(merged.Days, Has.Count.EqualTo(3)); // no duplicate of the replaced day
    }

    [Test]
    public void Merge_ProducesDistinctSortedRooms_AndSortsDays()
    {
        var existing = new SummaryDocument();
        var recomputed = new[]
        {
            Day("2026-06-17", "Shed"),
            Day("2026-06-16", "Office 1"),
            Day("2026-06-17", "Office 1"),
        };

        var store = new SummaryStore(Path.GetTempPath());
        var merged = store.Merge(existing, recomputed, new HashSet<string> { "2026-06-16", "2026-06-17" });

        Assert.That(merged.Rooms, Is.EqualTo(new[] { "Office 1", "Shed" })); // distinct + ordinal sort
        Assert.That(merged.Days.Select(d => (d.Date, d.Room)), Is.EqualTo(new[]
        {
            ("2026-06-16", "Office 1"),
            ("2026-06-17", "Office 1"),
            ("2026-06-17", "Shed"),
        }));
    }

    [Test]
    public void SummaryRoundTripsThroughDisk()
    {
        using var tmp = new TempDir();
        var store = new SummaryStore(tmp.Path);
        var doc = new SummaryDocument
        {
            GeneratedUtc = new DateTime(2026, 6, 17, 12, 0, 0, DateTimeKind.Utc),
            Rooms = ["Office 1"],
            Days = [Day("2026-06-17", "Office 1")],
        };

        store.SaveSummary(doc);
        var loaded = store.LoadSummary();

        Assert.That(loaded.Rooms, Is.EqualTo(new[] { "Office 1" }));
        Assert.That(loaded.Days, Has.Count.EqualTo(1));
        Assert.That(loaded.Days[0].Temperature!.Avg, Is.EqualTo(2));
    }

    [Test]
    public void StateRoundTripsThroughDisk()
    {
        using var tmp = new TempDir();
        var store = new SummaryStore(tmp.Path);

        store.SaveState(new StateDocument { LastProcessedDate = "2026-06-17" });

        Assert.That(store.LoadState().LastProcessedDate, Is.EqualTo("2026-06-17"));
    }

    [Test]
    public void LoadReturnsEmptyDocsWhenFilesMissing()
    {
        using var tmp = new TempDir();
        var store = new SummaryStore(tmp.Path);

        Assert.That(store.LoadSummary().Days, Is.Empty);
        Assert.That(store.LoadState().LastProcessedDate, Is.Null);
    }
}
