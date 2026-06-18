using System.Globalization;
using DeconzSummarized.Config;
using DeconzSummarized.Git;
using DeconzSummarized.Parsing;
using DeconzSummarized.Render;
using DeconzSummarized.Summarize;
using LibGit2Sharp;

namespace DeconzSummarized.Application;

/// <summary>
/// Orchestrates the end-to-end run: sync data, parse new days, aggregate, merge,
/// render the page and (optionally) push the site. Assumes a validated config.
/// </summary>
public sealed class SummaryPipeline
{
    private readonly AppConfig _config;
    private readonly Action<string> _log;

    public SummaryPipeline(AppConfig config, Action<string>? log = null)
    {
        _config = config;
        _log = log ?? (m => Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {m}"));
    }

    /// <summary>Runs the pipeline and returns a process exit code (0 = success / nothing to do).</summary>
    public int Run()
    {
        var config = _config;
        var signature = new Signature(config.CommitAuthorName, config.CommitAuthorEmail, DateTimeOffset.Now);

        // 1. Resolve the data source (local dir for offline mode, otherwise a clone/pull of the data repo).
        string dataDir;
        if (config.OfflineMode)
        {
            dataDir = config.UseLocalDataDir;
            _log($"Offline mode: reading data from '{dataDir}'.");
        }
        else
        {
            dataDir = config.DataClonePath;
            _log($"Syncing data repo into '{dataDir}'...");
            new RepoSync(config.EffectiveDataUser, config.DataRepoPat)
                .CloneOrPull(config.DataRepoUrl, dataDir, signature);
        }

        // 2. Resolve the site directory. When pushing, clone/pull the target repo so we keep prior
        //    state and can push back; otherwise just generate locally.
        var siteDir = config.SiteClonePath;
        RepoSync? siteSync = null;
        if (config.PushChanges)
        {
            siteSync = new RepoSync(config.EffectiveTargetUser, config.TargetRepoPat);
            _log($"Syncing target repo into '{siteDir}'...");
            siteSync.CloneOrPull(config.TargetRepoUrl, siteDir, signature);
        }
        else
        {
            Directory.CreateDirectory(siteDir);
            _log($"Push disabled: generating site locally in '{siteDir}'.");
        }

        // 3. Load prior summary + state.
        var store = new SummaryStore(siteDir);
        var summary = store.LoadSummary();
        var state = store.LoadState();

        // 4. Determine which day files to (re)process: everything on/after the last processed day
        //    (the latest day is reprocessed because its file grows during the day).
        var csvDir = config.ResolveCsvDir(dataDir);
        if (!Directory.Exists(csvDir))
        {
            _log($"CSV directory '{csvDir}' not found. Nothing to do.");
            return 0;
        }

        var allFiles = EnumerateDayFiles(csvDir).ToList();
        if (allFiles.Count == 0)
        {
            _log("No data CSV files found. Nothing to do.");
            return 0;
        }

        var candidates = state.LastProcessedDate is { } last
            ? allFiles.Where(f => string.CompareOrdinal(f.Date, last) >= 0).ToList()
            : allFiles;

        _log($"Found {allFiles.Count} day file(s); processing {candidates.Count}.");

        // 5. Parse + aggregate the candidate days.
        var readings = candidates.SelectMany(f => CsvSensorParser.ParseFile(f.Path, config.MaxStaleness)).ToList();
        var recomputed = DailyAggregator.Aggregate(readings);
        var reprocessedDates = candidates.Select(f => f.Date).ToHashSet();

        summary = store.Merge(summary, recomputed, reprocessedDates);
        store.SaveSummary(summary);

        state.LastProcessedDate = allFiles.Max(f => f.Date);
        store.SaveState(state);

        _log($"Summary now holds {summary.Days.Count} daily records across {summary.Rooms.Count} rooms.");

        // 6. Render the static page assets.
        new PageRenderer().Render(siteDir);

        // 7. Push if configured.
        if (config.PushChanges && siteSync is not null)
        {
            var message = $"Update sensor summary ({DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC)";
            var pushed = siteSync.CommitAndPush(siteDir, message, config.TargetBranch, signature);
            _log(pushed ? "Committed and pushed changes to target repo." : "No changes to push.");
        }
        else
        {
            _log($"Done. Open '{Path.Combine(siteDir, "index.html")}' to preview.");
        }

        return 0;
    }

    private static IEnumerable<(string Path, string Date)> EnumerateDayFiles(string dir)
    {
        foreach (var path in Directory.EnumerateFiles(dir, "*.csv"))
        {
            var name = Path.GetFileNameWithoutExtension(path);
            if (DateTime.TryParseExact(name, "yyyyMMdd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var date))
            {
                yield return (path, date.ToString("yyyy-MM-dd"));
            }
        }
    }
}
