namespace DeconzSummarized.Parsing;

/// <summary>A weather metric tracked from the deCONZ sensors.</summary>
public enum Metric
{
    Temperature,
    Humidity,
    Pressure
}

/// <summary>
/// A single parsed sensor reading from one snapshot row.
/// Values are already scaled to real-world units
/// (°C, %RH, hPa). Battery is the device battery percentage (0-100).
/// </summary>
public sealed record SensorReading(
    string Room,
    Metric Metric,
    double Value,
    DateTime TimestampUtc,
    int? BatteryPct);
