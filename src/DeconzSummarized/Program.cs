using System.Globalization;
using DeconzSummarized.Config;
using DeconzSummarized.Git;
using DeconzSummarized.Parsing;
using DeconzSummarized.Render;
using DeconzSummarized.Summarize;
using LibGit2Sharp;

var config = AppConfig.Load(AppContext.BaseDirectory);

try
{
    config.Validate();
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}

var signature = new Signature(config.CommitAuthorName, config.CommitAuthorEmail, DateTimeOffset.Now);

// 1. Resolve the data source (local dir for offline mode, otherwise a clone/pull of the data repo).
string dataDir;
if (config.OfflineMode)
{
    dataDir = config.UseLocalDataDir;
    Log($"Offline mode: reading data from '{dataDir}'.");
}
else
{
    dataDir = config.DataClonePath;
    Log($"Syncing data repo into '{dataDir}'...");
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
    Log($"Syncing target repo into '{siteDir}'...");
    siteSync.CloneOrPull(config.TargetRepoUrl, siteDir, signature);
}
else
{
    Directory.CreateDirectory(siteDir);
    Log($"Push disabled: generating site locally in '{siteDir}'.");
}

// 3. Load prior summary + state.
var store = new SummaryStore(siteDir);
var summary = store.LoadSummary();
var state = store.LoadState();

// 4. Determine which day files to (re)process: everything on/after the last processed day
//    (the latest day is reprocessed because its file grows during the day).
var allFiles = EnumerateDayFiles(dataDir).ToList();
if (allFiles.Count == 0)
{
    Log("No data CSV files found. Nothing to do.");
    return 0;
}

var candidates = state.LastProcessedDate is { } last
    ? allFiles.Where(f => string.CompareOrdinal(f.Date, last) >= 0).ToList()
    : allFiles;

Log($"Found {allFiles.Count} day file(s); processing {candidates.Count}.");

// 5. Parse + aggregate the candidate days.
var readings = candidates.SelectMany(f => CsvSensorParser.ParseFile(f.Path)).ToList();
var recomputed = DailyAggregator.Aggregate(readings);
var reprocessedDates = candidates.Select(f => f.Date).ToHashSet();

summary = store.Merge(summary, recomputed, reprocessedDates);
store.SaveSummary(summary);

state.LastProcessedDate = allFiles.Max(f => f.Date);
store.SaveState(state);

Log($"Summary now holds {summary.Days.Count} daily records across {summary.Rooms.Count} rooms.");

// 6. Render the static page assets.
new PageRenderer().Render(siteDir);

// 7. Push if configured.
if (config.PushChanges && siteSync is not null)
{
    var message = $"Update sensor summary ({DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC)";
    var pushed = siteSync.CommitAndPush(siteDir, message, config.TargetBranch, signature);
    Log(pushed ? "Committed and pushed changes to target repo." : "No changes to push.");
}
else
{
    Log($"Done. Open '{Path.Combine(siteDir, "index.html")}' to preview.");
}

return 0;

// ---- helpers ---------------------------------------------------------------

static IEnumerable<(string Path, string Date)> EnumerateDayFiles(string dir)
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

static void Log(string message) => Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {message}");
