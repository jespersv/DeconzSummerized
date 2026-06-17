using DeconzSummarized.Parsing;

namespace DeconzSummarized.Summarize;

/// <summary>Turns raw readings into per-(date, room) daily aggregates.</summary>
public static class DailyAggregator
{
    public static List<DailySummary> Aggregate(IEnumerable<SensorReading> readings)
    {
        var byDayRoom = readings
            .GroupBy(r => (Date: r.TimestampUtc.ToString("yyyy-MM-dd"), r.Room));

        var result = new List<DailySummary>();

        foreach (var group in byDayRoom)
        {
            var summary = new DailySummary
            {
                Date = group.Key.Date,
                Room = group.Key.Room,
                Temperature = StatFor(group, Metric.Temperature, decimals: 2),
                Humidity = StatFor(group, Metric.Humidity, decimals: 2),
                Pressure = StatFor(group, Metric.Pressure, decimals: 1),
                Battery = LatestBattery(group),
            };
            result.Add(summary);
        }

        return result;
    }

    private static MetricStat? StatFor(IEnumerable<SensorReading> readings, Metric metric, int decimals)
    {
        var values = readings.Where(r => r.Metric == metric).Select(r => r.Value).ToList();
        if (values.Count == 0)
            return null;

        return new MetricStat
        {
            Min = Math.Round(values.Min(), decimals),
            Avg = Math.Round(values.Average(), decimals),
            Max = Math.Round(values.Max(), decimals),
            Count = values.Count,
        };
    }

    private static int? LatestBattery(IEnumerable<SensorReading> readings)
    {
        return readings
            .Where(r => r.BatteryPct is not null)
            .OrderByDescending(r => r.TimestampUtc)
            .Select(r => r.BatteryPct)
            .FirstOrDefault();
    }
}
