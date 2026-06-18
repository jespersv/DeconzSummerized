using DeconzSummarized.Config;

namespace DeconzSummarized.Tests;

[TestFixture]
public class AppConfigTests
{
    [Test]
    public void ResolveCsvDir_AppendsSubDir_OrReturnsRootWhenEmpty()
    {
        var cfg = new AppConfig { DataSubDir = "hourly" };
        Assert.That(cfg.ResolveCsvDir("root"), Is.EqualTo(Path.Combine("root", "hourly")));

        cfg.DataSubDir = "";
        Assert.That(cfg.ResolveCsvDir("root"), Is.EqualTo("root"));
    }

    [Test]
    public void MaxStaleness_IsNullWhenDisabled_OtherwiseHours()
    {
        Assert.That(new AppConfig { StaleReadingHours = 0 }.MaxStaleness, Is.Null);
        Assert.That(new AppConfig { StaleReadingHours = 24 }.MaxStaleness, Is.EqualTo(TimeSpan.FromHours(24)));
    }

    [Test]
    public void EffectiveUsers_FallBackToGitHubUser()
    {
        var cfg = new AppConfig { GitHubUser = "shared" };
        Assert.That(cfg.EffectiveDataUser, Is.EqualTo("shared"));
        Assert.That(cfg.EffectiveTargetUser, Is.EqualTo("shared"));

        cfg.DataRepoUser = "data-user";
        cfg.TargetRepoUser = "site-user";
        Assert.That(cfg.EffectiveDataUser, Is.EqualTo("data-user"));
        Assert.That(cfg.EffectiveTargetUser, Is.EqualTo("site-user"));
    }

    [Test]
    public void OfflineMode_TrueOnlyWhenLocalDataDirSet()
    {
        Assert.That(new AppConfig().OfflineMode, Is.False);
        Assert.That(new AppConfig { UseLocalDataDir = "some/dir" }.OfflineMode, Is.True);
    }

    [Test]
    public void Validate_Passes_ForValidOnlinePushConfig()
    {
        var cfg = new AppConfig
        {
            DataRepoUrl = "https://example/data",
            TargetRepoUrl = "https://example/site",
            GitHubUser = "user",
            DataRepoPat = "pat1",
            TargetRepoPat = "pat2",
            PushChanges = true,
        };

        Assert.DoesNotThrow(() => cfg.Validate());
    }

    [Test]
    public void Validate_Fails_WhenOnlineDataPatMissing()
    {
        var cfg = new AppConfig
        {
            DataRepoUrl = "https://example/data",
            TargetRepoUrl = "https://example/site",
            GitHubUser = "user",
            PushChanges = false, // isolate the data-PAT error
        };

        var ex = Assert.Throws<InvalidOperationException>(() => cfg.Validate());
        Assert.That(ex!.Message, Does.Contain("DataRepoPat"));
    }

    [Test]
    public void Validate_Fails_WhenOfflineDataDirMissing()
    {
        var cfg = new AppConfig
        {
            UseLocalDataDir = Path.Combine(Path.GetTempPath(), "definitely-missing-" + Guid.NewGuid()),
            TargetRepoUrl = "https://example/site",
            PushChanges = false,
        };

        var ex = Assert.Throws<InvalidOperationException>(() => cfg.Validate());
        Assert.That(ex!.Message, Does.Contain("UseLocalDataDir"));
    }

    [Test]
    public void Validate_Fails_WhenPushEnabledButTargetPatMissing()
    {
        using var tmp = new TempDir();
        var cfg = new AppConfig
        {
            UseLocalDataDir = tmp.Path, // offline, so data side is fine
            TargetRepoUrl = "https://example/site",
            GitHubUser = "user",
            PushChanges = true, // requires target PAT
        };

        var ex = Assert.Throws<InvalidOperationException>(() => cfg.Validate());
        Assert.That(ex!.Message, Does.Contain("TargetRepoPat"));
    }
}
