using System.Text.Json;
using DeconzSummarized.Application;
using DeconzSummarized.Config;

namespace DeconzSummarized.Tests;

[TestFixture]
public class SummaryPipelineTests
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    /// <summary>Offline + no-push config rooted under a temp dir (touches no git/network).</summary>
    private static AppConfig OfflineConfig(TempDir tmp) => new()
    {
        UseLocalDataDir = Path.Combine(tmp.Path, "datasrc"),
        WorkDir = Path.Combine(tmp.Path, "work"),
        DataSubDir = "hourly",
        PushChanges = false,
        StaleReadingHours = 24,
    };

    private static void WriteFixtures(TempDir tmp)
    {
        // Two days, two healthy rooms, one Daylight row (must be excluded) and one stale
        // Attic row (must be dropped by the 24h staleness filter).
        CsvFixture.WriteFile(tmp.File(Path.Combine("datasrc", "hourly", "20260616.csv")),
            CsvFixture.Row("sensorfetch_202606160100.json", "Daylight", "Daylight"),
            CsvFixture.Row("sensorfetch_202606160100.json", "Office 1", "ZHATemperature", temperature: 2500),
            CsvFixture.Row("sensorfetch_202606160100.json", "Office 1", "ZHAHumidity", humidity: 4000),
            CsvFixture.Row("sensorfetch_202606160100.json", "Office 1", "ZHAPressure", pressure: 1009),
            CsvFixture.Row("sensorfetch_202606160100.json", "Shed", "ZHATemperature", temperature: 1500),
            CsvFixture.Row("sensorfetch_202606160100.json", "Attic", "ZHATemperature",
                temperature: 72, lastUpdated: "2025-11-15T01:29:45.393"));

        CsvFixture.WriteFile(tmp.File(Path.Combine("datasrc", "hourly", "20260617.csv")),
            CsvFixture.Row("sensorfetch_202606170100.json", "Office 1", "ZHATemperature", temperature: 2601),
            CsvFixture.Row("sensorfetch_202606170100.json", "Shed", "ZHATemperature", temperature: 1600));
    }

    private static SummaryDoc ReadSummary(AppConfig cfg)
    {
        var path = Path.Combine(cfg.SiteClonePath, "summary.json");
        return JsonSerializer.Deserialize<SummaryDoc>(File.ReadAllText(path), Json)!;
    }

    [Test]
    public void Run_GeneratesSiteAssets_AndSummaryAndState()
    {
        using var tmp = new TempDir();
        WriteFixtures(tmp);
        var cfg = OfflineConfig(tmp);

        var exit = new SummaryPipeline(cfg, _ => { }).Run();

        Assert.That(exit, Is.EqualTo(0));

        var site = cfg.SiteClonePath;
        foreach (var asset in new[] { "summary.json", "state.json", "index.html", "app.js", "styles.css", ".nojekyll" })
            Assert.That(File.Exists(Path.Combine(site, asset)), Is.True, $"missing {asset}");

        var summary = ReadSummary(cfg);

        // Daylight excluded + stale Attic dropped => only Office 1 and Shed remain.
        Assert.That(summary.Rooms, Is.EqualTo(new[] { "Office 1", "Shed" }));
        // 2 rooms x 2 days = 4 daily records.
        Assert.That(summary.Days, Has.Count.EqualTo(4));

        var office17 = summary.Days.Single(d => d.Date == "2026-06-17" && d.Room == "Office 1");
        Assert.That(office17.Temperature!.Avg, Is.EqualTo(26.01)); // 2601 scaled

        var state = JsonSerializer.Deserialize<StateDoc>(
            File.ReadAllText(Path.Combine(site, "state.json")), Json)!;
        Assert.That(state.LastProcessedDate, Is.EqualTo("2026-06-17"));
    }

    [Test]
    public void Run_Twice_IsIncremental_NoDuplicatesAndHistoryKept()
    {
        using var tmp = new TempDir();
        WriteFixtures(tmp);
        var cfg = OfflineConfig(tmp);

        new SummaryPipeline(cfg, _ => { }).Run();
        new SummaryPipeline(cfg, _ => { }).Run();

        var summary = ReadSummary(cfg);

        // Still exactly 4 records — the second run reprocessed the latest day in place.
        Assert.That(summary.Days, Has.Count.EqualTo(4));
        var keys = summary.Days.Select(d => (d.Date, d.Room)).ToList();
        Assert.That(keys, Is.Unique);
        // Earliest day's history is preserved.
        Assert.That(summary.Days.Any(d => d.Date == "2026-06-16"), Is.True);
    }

    // Minimal DTOs mirroring the emitted JSON (camelCase).
    private sealed record SummaryDoc(List<string> Rooms, List<DayRec> Days);
    private sealed record DayRec(string Date, string Room, Stat? Temperature);
    private sealed record Stat(double Min, double Avg, double Max);
    private sealed record StateDoc(string? LastProcessedDate);
}
