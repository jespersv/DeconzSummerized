namespace DeconzSummarized.Tests;

/// <summary>A throwaway temp directory that deletes itself on dispose.</summary>
public sealed class TempDir : IDisposable
{
    public string Path { get; }

    public TempDir()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "deconz-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    /// <summary>Combines a relative path under the temp dir, creating parent folders.</summary>
    public string File(string relative)
    {
        var full = System.IO.Path.Combine(Path, relative);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full)!);
        return full;
    }

    public void Dispose()
    {
        try { Directory.Delete(Path, recursive: true); }
        catch { /* best effort cleanup */ }
    }
}

/// <summary>Builds deCONZ CSV fixtures with the real 21-column header.</summary>
public static class CsvFixture
{
    public const string Header =
        "FileName;UniqueId;On;Battery;SunriseOffset;SunsetOffset;Dark;Daylight;Pressure;Humidity;Temperature;LastUpdated;Status;Sunrise;Sunset;Etag;Manufacturername;Modelid;Name;SwVersion;Type";

    /// <summary>
    /// Builds one CSV row. Snapshot time comes from <paramref name="fileName"/>; the value
    /// column used depends on <paramref name="type"/>.
    /// </summary>
    public static string Row(
        string fileName,
        string name,
        string type,
        int pressure = 0,
        int humidity = 0,
        int temperature = 0,
        int battery = 100,
        string lastUpdated = "",
        string uniqueId = "00:15:8d:00:00:00:00:01-01-0402")
    {
        // Column order matches Header.
        return string.Join(';',
            fileName, uniqueId, "True", battery, "0", "0", "False", "False",
            pressure, humidity, temperature, lastUpdated, "0", "", "", "etag",
            "LUMI", "lumi.weather", name, "20191205", type);
    }

    /// <summary>Writes a CSV file (header + rows) and returns its path.</summary>
    public static string WriteFile(string path, params string[] rows)
    {
        var content = string.Join(Environment.NewLine, new[] { Header }.Concat(rows));
        File.WriteAllText(path, content);
        return path;
    }
}
