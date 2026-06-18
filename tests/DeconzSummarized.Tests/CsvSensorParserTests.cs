using DeconzSummarized.Parsing;

namespace DeconzSummarized.Tests;

[TestFixture]
public class CsvSensorParserTests
{
    [Test]
    public void ScalesTemperatureAndHumidity_ButLeavesPressureAsIs()
    {
        using var tmp = new TempDir();
        var path = CsvFixture.WriteFile(tmp.File("20260617.csv"),
            CsvFixture.Row("sensorfetch_202606170100.json", "Office 1", "ZHATemperature", temperature: 2601),
            CsvFixture.Row("sensorfetch_202606170100.json", "Office 1", "ZHAHumidity", humidity: 7857),
            CsvFixture.Row("sensorfetch_202606170100.json", "Office 1", "ZHAPressure", pressure: 1009));

        var readings = CsvSensorParser.ParseFile(path).ToList();

        Assert.That(readings, Has.Count.EqualTo(3));
        Assert.That(readings.Single(r => r.Metric == Metric.Temperature).Value, Is.EqualTo(26.01));
        Assert.That(readings.Single(r => r.Metric == Metric.Humidity).Value, Is.EqualTo(78.57));
        Assert.That(readings.Single(r => r.Metric == Metric.Pressure).Value, Is.EqualTo(1009));
    }

    [Test]
    public void SkipsDaylightAndUnknownSensorTypes()
    {
        using var tmp = new TempDir();
        var path = CsvFixture.WriteFile(tmp.File("20260617.csv"),
            CsvFixture.Row("sensorfetch_202606170100.json", "Daylight", "Daylight"),
            CsvFixture.Row("sensorfetch_202606170100.json", "Office 1", "ZHASomethingElse", temperature: 100),
            CsvFixture.Row("sensorfetch_202606170100.json", "Office 1", "ZHATemperature", temperature: 2000));

        var readings = CsvSensorParser.ParseFile(path).ToList();

        Assert.That(readings, Has.Count.EqualTo(1));
        Assert.That(readings[0].Metric, Is.EqualTo(Metric.Temperature));
    }

    [Test]
    public void DerivesTimestampFromFileNameAsUtc()
    {
        using var tmp = new TempDir();
        var path = CsvFixture.WriteFile(tmp.File("20260617.csv"),
            CsvFixture.Row("sensorfetch_202606170100.json", "Office 1", "ZHATemperature", temperature: 2000));

        var reading = CsvSensorParser.ParseFile(path).Single();

        Assert.That(reading.TimestampUtc, Is.EqualTo(new DateTime(2026, 6, 17, 1, 0, 0, DateTimeKind.Utc)));
        Assert.That(reading.TimestampUtc.Kind, Is.EqualTo(DateTimeKind.Utc));
        Assert.That(reading.Room, Is.EqualTo("Office 1"));
    }

    [Test]
    public void ParsesBatteryPercentage()
    {
        using var tmp = new TempDir();
        var path = CsvFixture.WriteFile(tmp.File("20260617.csv"),
            CsvFixture.Row("sensorfetch_202606170100.json", "Office 1", "ZHATemperature", temperature: 2000, battery: 73));

        var reading = CsvSensorParser.ParseFile(path).Single();

        Assert.That(reading.BatteryPct, Is.EqualTo(73));
    }

    [Test]
    public void DropsStaleReadings_WhenLastUpdatedLagsBeyondLimit()
    {
        using var tmp = new TempDir();
        var path = CsvFixture.WriteFile(tmp.File("20260617.csv"),
            // Fresh: LastUpdated minutes before the snapshot.
            CsvFixture.Row("sensorfetch_202606170100.json", "Office 1", "ZHATemperature",
                temperature: 2000, lastUpdated: "2026-06-17T00:50:00.000"),
            // Stale: LastUpdated months before the snapshot.
            CsvFixture.Row("sensorfetch_202606170100.json", "Attic", "ZHATemperature",
                temperature: 72, lastUpdated: "2025-11-15T01:29:45.393"));

        var withFilter = CsvSensorParser.ParseFile(path, TimeSpan.FromHours(24)).ToList();
        var noFilter = CsvSensorParser.ParseFile(path, maxStaleness: null).ToList();

        Assert.That(withFilter.Select(r => r.Room), Is.EquivalentTo(new[] { "Office 1" }));
        Assert.That(noFilter.Select(r => r.Room), Is.EquivalentTo(new[] { "Office 1", "Attic" }));
    }

    [Test]
    public void SkipsMalformedRowsWithoutThrowing()
    {
        using var tmp = new TempDir();
        // Second line is short/garbage; it must be skipped, not throw.
        var path = tmp.File("20260617.csv");
        File.WriteAllText(path, string.Join(Environment.NewLine,
            CsvFixture.Header,
            "this;is;not;valid",
            CsvFixture.Row("sensorfetch_202606170100.json", "Office 1", "ZHATemperature", temperature: 2000)));

        var readings = CsvSensorParser.ParseFile(path).ToList();

        Assert.That(readings, Has.Count.EqualTo(1));
        Assert.That(readings[0].Room, Is.EqualTo("Office 1"));
    }

    [Test]
    public void RowWithUnparseableFileName_IsSkipped()
    {
        using var tmp = new TempDir();
        var path = CsvFixture.WriteFile(tmp.File("20260617.csv"),
            CsvFixture.Row("not_a_snapshot.json", "Office 1", "ZHATemperature", temperature: 2000));

        var readings = CsvSensorParser.ParseFile(path).ToList();

        Assert.That(readings, Is.Empty);
    }
}
