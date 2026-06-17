namespace DeconzSummarized.Render;

/// <summary>Copies the static page assets (index.html, app.js, styles.css) into the site directory.</summary>
public sealed class PageRenderer
{
    private static readonly string[] Assets = { "index.html", "app.js", "styles.css" };

    private readonly string _templatesDir;

    public PageRenderer()
    {
        _templatesDir = Path.Combine(AppContext.BaseDirectory, "templates");
    }

    /// <summary>Writes/refreshes the page assets in <paramref name="siteDir"/>. summary.json is produced separately.</summary>
    public void Render(string siteDir)
    {
        Directory.CreateDirectory(siteDir);

        foreach (var asset in Assets)
        {
            var src = Path.Combine(_templatesDir, asset);
            var dst = Path.Combine(siteDir, asset);
            File.Copy(src, dst, overwrite: true);
        }

        // Serve files as-is (no Jekyll processing) on GitHub Pages.
        File.WriteAllText(Path.Combine(siteDir, ".nojekyll"), "");
    }
}
