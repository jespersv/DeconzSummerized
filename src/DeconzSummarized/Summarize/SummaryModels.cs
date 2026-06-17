namespace DeconzSummarized.Summarize;

/// <summary>Min/avg/max for one metric over one day in one room.</summary>
public sealed class MetricStat
{
    public double Min { get; set; }
    public double Avg { get; set; }
    public double Max { get; set; }
    public int Count { get; set; }
}

/// <summary>Aggregated values for one (date, room) pair.</summary>
public sealed class DailySummary
{
    /// <summary>Calendar day in yyyy-MM-dd (UTC).</summary>
    public string Date { get; set; } = "";
    public string Room { get; set; } = "";

    public MetricStat? Temperature { get; set; }
    public MetricStat? Humidity { get; set; }
    public MetricStat? Pressure { get; set; }

    /// <summary>Latest battery percentage seen that day for the room.</summary>
    public int? Battery { get; set; }
}

/// <summary>The full summary document written to the target repo as summary.json.</summary>
public sealed class SummaryDocument
{
    public DateTime GeneratedUtc { get; set; }
    public List<string> Rooms { get; set; } = new();
    public List<DailySummary> Days { get; set; } = new();
}

/// <summary>Incremental processing marker, stored as state.json in the target repo.</summary>
public sealed class StateDocument
{
    /// <summary>Most recent data day processed, yyyy-MM-dd. Null on first run.</summary>
    public string? LastProcessedDate { get; set; }
}
