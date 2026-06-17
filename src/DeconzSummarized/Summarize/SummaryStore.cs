using System.Text.Json;

namespace DeconzSummarized.Summarize;

/// <summary>Loads, merges and persists summary.json and state.json in the target site directory.</summary>
public sealed class SummaryStore
{
    public const string SummaryFileName = "summary.json";
    public const string StateFileName = "state.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly string _summaryPath;
    private readonly string _statePath;

    public SummaryStore(string siteDir)
    {
        _summaryPath = Path.Combine(siteDir, SummaryFileName);
        _statePath = Path.Combine(siteDir, StateFileName);
    }

    public SummaryDocument LoadSummary()
    {
        if (!File.Exists(_summaryPath))
            return new SummaryDocument();

        var json = File.ReadAllText(_summaryPath);
        return JsonSerializer.Deserialize<SummaryDocument>(json, JsonOptions) ?? new SummaryDocument();
    }

    public StateDocument LoadState()
    {
        if (!File.Exists(_statePath))
            return new StateDocument();

        var json = File.ReadAllText(_statePath);
        return JsonSerializer.Deserialize<StateDocument>(json, JsonOptions) ?? new StateDocument();
    }

    /// <summary>
    /// Replaces all entries for the reprocessed dates with the freshly computed ones,
    /// then refreshes the room list, sort order and generation timestamp.
    /// </summary>
    public SummaryDocument Merge(SummaryDocument existing, IReadOnlyCollection<DailySummary> recomputed,
        IReadOnlySet<string> reprocessedDates)
    {
        var kept = existing.Days.Where(d => !reprocessedDates.Contains(d.Date));

        var merged = kept
            .Concat(recomputed)
            .OrderBy(d => d.Date, StringComparer.Ordinal)
            .ThenBy(d => d.Room, StringComparer.Ordinal)
            .ToList();

        return new SummaryDocument
        {
            GeneratedUtc = DateTime.UtcNow,
            Rooms = merged.Select(d => d.Room).Distinct().OrderBy(r => r, StringComparer.Ordinal).ToList(),
            Days = merged,
        };
    }

    public void SaveSummary(SummaryDocument doc) =>
        File.WriteAllText(_summaryPath, JsonSerializer.Serialize(doc, JsonOptions));

    public void SaveState(StateDocument state) =>
        File.WriteAllText(_statePath, JsonSerializer.Serialize(state, JsonOptions));
}
