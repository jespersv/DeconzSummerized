# DeconzSummarized

A .NET 8 console job that turns raw deCONZ sensor logs into a static, year-over-year
charts dashboard published via GitHub Pages.

## Flow

1. Clone/pull the **data repo** (`jespersv/DeconzResult`) with LibGit2Sharp.
2. Incrementally parse the daily CSV snapshots (only new/updated days).
3. Aggregate to per-day, per-room min/avg/max for temperature, humidity, pressure,
   plus latest battery.
4. Merge into `summary.json` and render a static page (`index.html` + `app.js` +
   `styles.css`, Chart.js via CDN).
5. Commit & push everything to the **target repo** (`jespersv/home-temp`), where
   GitHub Pages serves it.

State (`state.json`) and the aggregated `summary.json` live in the target repo, so
each run only reprocesses the latest day onward.

## Data format

The data repo holds one semicolon-delimited CSV per day, named `YYYYMMDD.csv`,
inside a `hourly/` subfolder (configurable via `DataSubDir`).
Each row is one sensor reading from an hourly snapshot:

- Snapshot time comes from `FileName` (`sensorfetch_YYYYMMDDHHMM.json`).
- Each `lumi.weather` device emits 3 rows per snapshot (`Type` =
  `ZHATemperature` / `ZHAHumidity` / `ZHAPressure`), sharing the `Name` (room).
- `Temperature` and `Humidity` are integers ×100 (`2601` = 26.01 °C); `Pressure`
  is hPa; `Battery` is 0–100 %.
- The Philips `Daylight` sensor is ignored.

Sample files are in [`examples_data/hourly/`](examples_data/hourly) for local testing
(mirroring the real repo layout).

## Configuration

Non-secret settings live in
[`src/DeconzSummarized/appsettings.json`](src/DeconzSummarized/appsettings.json).
Any key can be overridden by an environment variable of the same name, or by a
gitignored `appsettings.local.json`.

| Setting | Meaning |
| --- | --- |
| `DataRepoUrl` / `TargetRepoUrl` | The two GitHub repos. |
| `GitHubUser` | Default username for both repos (override per-repo if needed). |
| `WorkDir` | Local working dir for the clones (gitignored, default `work`). |
| `DataSubDir` | Subfolder in the data repo holding the CSVs (default `hourly`; empty = root). |
| `StaleReadingHours` | Drop readings whose `LastUpdated` lags the snapshot by more than this (default `24`, `0` disables). |
| `TargetBranch` | Branch GitHub Pages serves from (default `main`). |
| `UseLocalDataDir` | If set, read CSVs from this path and skip cloning the data repo. |
| `PushChanges` | Set `false` to generate the site locally without pushing. |

### Secrets (PATs)

Provide a Personal Access Token for each repo via environment variables (preferred —
never commit them):

```powershell
$env:DECONZ_DATA_PAT = "<PAT with read access to DeconzResult>"
$env:DECONZ_SITE_PAT = "<PAT with write access to home-temp>"
```

For local development you can also use `dotnet user-secrets`.

## Running

```powershell
# Normal run: pull data, summarize, push site to home-temp
dotnet run --project src/DeconzSummarized

# Offline dry run against the bundled sample data (no git, no push)
$env:UseLocalDataDir = "$PWD\examples_data"
$env:PushChanges = "false"
dotnet run --project src/DeconzSummarized
# Then preview:
python -m http.server 8000 --directory work/site   # open http://localhost:8000
```

Schedule the normal run however you like (e.g. Windows Task Scheduler).

## GitHub Pages setup (one time)

In the `home-temp` repo: **Settings → Pages → Build and deployment → Deploy from a
branch**, select the `main` branch and `/ (root)` folder. The dashboard will be at
`https://jespersv.github.io/home-temp/`.

## Project layout

```
src/DeconzSummarized/
  Program.cs                 # pipeline orchestration
  Config/AppConfig.cs        # settings + env binding
  Git/RepoSync.cs            # LibGit2Sharp clone/pull + commit/push
  Parsing/                   # SensorReading model + CSV parser
  Summarize/                 # daily aggregation + summary/state store
  Render/PageRenderer.cs     # copies page assets into the site dir
  templates/                 # index.html, app.js, styles.css
  appsettings.json
examples_data/hourly/        # sample CSVs for offline testing
```
