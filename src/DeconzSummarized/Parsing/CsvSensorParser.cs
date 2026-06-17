using System.Globalization;
using System.Text.RegularExpressions;
using CsvHelper;
using CsvHelper.Configuration;

namespace DeconzSummarized.Parsing;

/// <summary>
/// Parses a single deCONZ day CSV (semicolon-delimited) into <see cref="SensorReading"/>s.
/// Each physical lumi.weather device emits three rows per snapshot (ZHATemperature /
/// ZHAHumidity / ZHAPressure); the value is read from the column matching the row's Type.
/// Temperature and Humidity are stored as integers x100 in the source and are scaled back here.
/// </summary>
public static partial class CsvSensorParser
{
    [GeneratedRegex(@"sensorfetch_(\d{12})\.json", RegexOptions.IgnoreCase)]
    private static partial Regex FileNameTimestampRegex();

    private static readonly CsvConfiguration CsvConfig = new(CultureInfo.InvariantCulture)
    {
        Delimiter = ";",
        HasHeaderRecord = true,
        MissingFieldFound = null,
        BadDataFound = null,
        TrimOptions = TrimOptions.Trim,
    };

    /// <summary>Parses one CSV file, skipping malformed rows and non-weather (Daylight) sensors.</summary>
    public static IEnumerable<SensorReading> ParseFile(string path)
    {
        using var reader = new StreamReader(path);
        using var csv = new CsvReader(reader, CsvConfig);

        if (!csv.Read() || !csv.ReadHeader())
            yield break;

        while (csv.Read())
        {
            SensorReading? reading = TryParseRow(csv);
            if (reading is not null)
                yield return reading;
        }
    }

    private static SensorReading? TryParseRow(CsvReader csv)
    {
        try
        {
            var type = csv.GetField("Type");
            var room = csv.GetField("Name");
            if (string.IsNullOrWhiteSpace(type) || string.IsNullOrWhiteSpace(room))
                return null;

            Metric metric;
            double rawValue;
            switch (type)
            {
                case "ZHATemperature":
                    metric = Metric.Temperature;
                    rawValue = ParseDouble(csv.GetField("Temperature")) / 100.0;
                    break;
                case "ZHAHumidity":
                    metric = Metric.Humidity;
                    rawValue = ParseDouble(csv.GetField("Humidity")) / 100.0;
                    break;
                case "ZHAPressure":
                    metric = Metric.Pressure;
                    rawValue = ParseDouble(csv.GetField("Pressure"));
                    break;
                default:
                    // Daylight and any other non-weather sensor types are ignored.
                    return null;
            }

            DateTime? timestamp = ParseSnapshotTimestamp(csv.GetField("FileName"));
            if (timestamp is null)
                return null;

            int? battery = TryParseInt(csv.GetField("Battery"));

            return new SensorReading(room.Trim(), metric, rawValue, timestamp.Value, battery);
        }
        catch
        {
            // Tolerant of malformed rows — skip and continue.
            return null;
        }
    }

    /// <summary>Extracts the snapshot time from a FileName like "sensorfetch_202606170100.json".</summary>
    private static DateTime? ParseSnapshotTimestamp(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return null;

        Match m = FileNameTimestampRegex().Match(fileName);
        if (!m.Success)
            return null;

        if (DateTime.TryParseExact(m.Groups[1].Value, "yyyyMMddHHmm",
                CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var ts))
        {
            return ts;
        }

        return null;
    }

    private static double ParseDouble(string? s) =>
        double.Parse(s ?? "0", NumberStyles.Float, CultureInfo.InvariantCulture);

    private static int? TryParseInt(string? s) =>
        int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : null;
}
