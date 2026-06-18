using DeconzSummarized.Parsing;
using DeconzSummarized.Summarize;

namespace DeconzSummarized.Tests;

[TestFixture]
public class DailyAggregatorTests
{
    private static SensorReading R(string room, Metric metric, double value, DateTime ts, int? battery = null) =>
        new(room, metric, value, ts, battery);

    private static readonly DateTime Day = new(2026, 6, 17, 0, 0, 0, DateTimeKind.Utc);

    [Test]
    public void ComputesMinAvgMax_WithMetricRounding()
    {
        var readings = new[]
        {
            R("Office 1", Metric.Temperature, 25.28, Day.AddHours(1)),
            R("Office 1", Metric.Temperature, 25.55, Day.AddHours(2)),
            R("Office 1", Metric.Temperature, 26.01, Day.AddHours(3)),
            R("Office 1", Metric.Pressure, 1009.04, Day.AddHours(1)),
            R("Office 1", Metric.Pressure, 1011.10, Day.AddHours(2)),
        };

        var summary = DailyAggregator.Aggregate(readings).Single();

        Assert.That(summary.Temperature!.Min, Is.EqualTo(25.28));
        Assert.That(summary.Temperature.Max, Is.EqualTo(26.01));
        Assert.That(summary.Temperature.Avg, Is.EqualTo(Math.Round((25.28 + 25.55 + 26.01) / 3.0, 2)));
        Assert.That(summary.Temperature.Count, Is.EqualTo(3));

        // Pressure rounds to 1 decimal place (avg 1010.07 -> 1010.1).
        Assert.That(summary.Pressure!.Avg, Is.EqualTo(1010.1));
    }

    [Test]
    public void BatteryIsLatestReadingByTimestamp()
    {
        var readings = new[]
        {
            R("Office 1", Metric.Temperature, 20, Day.AddHours(1), battery: 90),
            R("Office 1", Metric.Temperature, 21, Day.AddHours(5), battery: 80),
            R("Office 1", Metric.Temperature, 22, Day.AddHours(3), battery: 85),
        };

        var summary = DailyAggregator.Aggregate(readings).Single();

        Assert.That(summary.Battery, Is.EqualTo(80));
    }

    [Test]
    public void MissingMetricYieldsNullStat()
    {
        var readings = new[] { R("Shed", Metric.Humidity, 65.0, Day.AddHours(1)) };

        var summary = DailyAggregator.Aggregate(readings).Single();

        Assert.That(summary.Humidity, Is.Not.Null);
        Assert.That(summary.Temperature, Is.Null);
        Assert.That(summary.Pressure, Is.Null);
    }

    [Test]
    public void GroupsByDateAndRoom()
    {
        var readings = new[]
        {
            R("Office 1", Metric.Temperature, 20, Day.AddHours(1)),
            R("Shed", Metric.Temperature, 15, Day.AddHours(1)),
            R("Office 1", Metric.Temperature, 21, Day.AddDays(1).AddHours(1)),
        };

        var summaries = DailyAggregator.Aggregate(readings);

        Assert.That(summaries, Has.Count.EqualTo(3));
        Assert.That(summaries.Select(s => (s.Date, s.Room)), Is.EquivalentTo(new[]
        {
            ("2026-06-17", "Office 1"),
            ("2026-06-17", "Shed"),
            ("2026-06-18", "Office 1"),
        }));
    }
}
