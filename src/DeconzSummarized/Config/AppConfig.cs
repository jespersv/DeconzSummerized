using Microsoft.Extensions.Configuration;

namespace DeconzSummarized.Config;

/// <summary>
/// Strongly-typed application configuration, bound from appsettings.json,
/// appsettings.local.json and environment variables. PATs should come from
/// the environment (DECONZ_DATA_PAT / DECONZ_SITE_PAT) and are never persisted.
/// </summary>
public sealed class AppConfig
{
    public string DataRepoUrl { get; set; } = "";
    public string TargetRepoUrl { get; set; } = "";

    /// <summary>Default GitHub username used for both repos unless overridden.</summary>
    public string GitHubUser { get; set; } = "";

    public string DataRepoUser { get; set; } = "";
    public string DataRepoPat { get; set; } = "";
    public string TargetRepoUser { get; set; } = "";
    public string TargetRepoPat { get; set; } = "";

    public string WorkDir { get; set; } = "work";
    public string DataDirName { get; set; } = "data";
    public string SiteDirName { get; set; } = "site";

    public string TargetBranch { get; set; } = "main";
    public string CommitAuthorName { get; set; } = "DeconzSummarized Bot";
    public string CommitAuthorEmail { get; set; } = "bot@users.noreply.github.com";

    /// <summary>When set, read CSVs from this directory and skip cloning the data repo.</summary>
    public string UseLocalDataDir { get; set; } = "";

    public bool PushChanges { get; set; } = true;

    // ---- Resolved helpers ------------------------------------------------

    public string EffectiveDataUser =>
        !string.IsNullOrWhiteSpace(DataRepoUser) ? DataRepoUser : GitHubUser;

    public string EffectiveTargetUser =>
        !string.IsNullOrWhiteSpace(TargetRepoUser) ? TargetRepoUser : GitHubUser;

    public string DataClonePath => Path.Combine(WorkDir, DataDirName);
    public string SiteClonePath => Path.Combine(WorkDir, SiteDirName);

    public bool OfflineMode => !string.IsNullOrWhiteSpace(UseLocalDataDir);

    /// <summary>Builds configuration from files + environment and binds an <see cref="AppConfig"/>.</summary>
    public static AppConfig Load(string basePath)
    {
        IConfiguration config = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: false)
            .AddEnvironmentVariables()
            .Build();

        var cfg = new AppConfig();
        config.Bind(cfg);

        // PATs from environment take precedence (preferred secret source).
        var dataPat = Environment.GetEnvironmentVariable("DECONZ_DATA_PAT");
        if (!string.IsNullOrWhiteSpace(dataPat)) cfg.DataRepoPat = dataPat;

        var sitePat = Environment.GetEnvironmentVariable("DECONZ_SITE_PAT");
        if (!string.IsNullOrWhiteSpace(sitePat)) cfg.TargetRepoPat = sitePat;

        return cfg;
    }

    /// <summary>Throws if required settings for the selected mode are missing.</summary>
    public void Validate()
    {
        var errors = new List<string>();

        if (OfflineMode)
        {
            if (!Directory.Exists(UseLocalDataDir))
                errors.Add($"UseLocalDataDir '{UseLocalDataDir}' does not exist.");
        }
        else
        {
            if (string.IsNullOrWhiteSpace(DataRepoUrl)) errors.Add("DataRepoUrl is required.");
            if (string.IsNullOrWhiteSpace(EffectiveDataUser)) errors.Add("A data repo username is required (GitHubUser or DataRepoUser).");
            if (string.IsNullOrWhiteSpace(DataRepoPat)) errors.Add("DataRepoPat is required (set DECONZ_DATA_PAT).");
        }

        if (string.IsNullOrWhiteSpace(TargetRepoUrl)) errors.Add("TargetRepoUrl is required.");

        if (PushChanges)
        {
            if (string.IsNullOrWhiteSpace(EffectiveTargetUser)) errors.Add("A target repo username is required (GitHubUser or TargetRepoUser).");
            if (string.IsNullOrWhiteSpace(TargetRepoPat)) errors.Add("TargetRepoPat is required to push (set DECONZ_SITE_PAT).");
        }

        if (errors.Count > 0)
            throw new InvalidOperationException("Invalid configuration:\n  - " + string.Join("\n  - ", errors));
    }
}
