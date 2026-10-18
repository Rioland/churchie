<p align="center">
  <img src="src/Churchie.App/Assets/churchie.png" width="96" alt="Churchie icon" />
</p>

# Churchie

Worship presentation software in the spirit of EasyWorship — song lyrics, scripture and pictures on the projector — written in **C#** with **Avalonia**, so the same app runs on **Windows, macOS and Linux**.

## What it does (version 0.1)

| Area | Features |
| --- | --- |
| **Layout** | Schedule (service order) · Preview · Live, with resource tabs underneath — the familiar EasyWorship arrangement |
| **Projector** | Full-screen output on the second screen (or a movable window when you only have one), **Black**, **Clear** (hide words, keep background) and **Logo** buttons |
| **Songs** | Library with search by title, words or author; editor that understands `[Verse 1]`, `[Chorus]`, `Bridge:`, `V2`…; import from `.txt` and OpenLyrics `.xml`; three public-domain hymns to start with |
| **Scripture** | King James Version built in; type references the way people say them (`John 3:16-18`, `jn 3 16`, `1 Cor 13:4-7`, `Ps 23`); browse by book and chapter; add more translations as Zefania XML files (e.g. Yoruba, Igbo, Hausa) |
| **Media** | Pictures as song or scripture backgrounds, as the logo, or shown full-screen |
| **Themes** | Separate looks for songs and scripture: size, font, colours, bold/shadow/uppercase, alignment, background picture with darkening — changes appear on screen instantly |
| **Safety** | The schedule autosaves after every change; long text automatically shrinks to fit the screen |

### Keyboard

| Key | Action |
| --- | --- |
| Page Down / Page Up | Next / previous slide (works with presentation clickers) |
| → ← or Space | Next / previous slide when the Live panel has focus |
| B · C · L | Black · Clear · Logo |
| Ctrl+Enter | Send the preview live |
| Ctrl+S · Ctrl+O · Ctrl+N | Save · open · new schedule |
| Esc / F11 (on the projector window) | Close / toggle full screen |

## Download

Every push to `main` builds the app on GitHub. Open the **Actions** tab → the latest **Build** run → **Artifacts**:

- `Churchie-win-x64` — unzip and run `Churchie.exe`
- `Churchie-osx-arm64` — Apple Silicon Macs (M1/M2/M3…); `Churchie-osx-x64` for Intel Macs
- `Churchie-linux-x64` — extract and run `./Churchie`

To make a proper release with download links, push a tag: `git tag v0.1.0 && git push --tags`.

> **macOS:** the app isn't notarised by Apple yet. The first time, right-click `Churchie.app` → **Open** → **Open**, or run `xattr -cr Churchie.app` in Terminal.

## Work on it from anywhere (no PC needed)

The repository includes a **GitHub Codespaces** setup, so you can code in a browser — even on a tablet — and actually see the app running.

1. On github.com open this repo → green **Code** button → **Codespaces** → **Create codespace on main**.
2. Wait for VS Code to open in the browser (first time takes a few minutes; it installs .NET 10 and restores packages).
3. Open the **Ports** tab and click the globe next to **6080 – App desktop**. Password: `churchie`.
4. Back in VS Code: **Terminal → Run Task… → run app** (or press F5). Churchie appears in the desktop tab.

Small edits can also be made straight on github.com (press `.` on the repo page to open the web editor); every push triggers a new build.

## Project layout

```
src/
  Churchie.Core/        Everything that isn't UI (no dependencies, fully unit-tested)
    Models/             Song, Slide, SlideTheme, Schedule, AppSettings
    Bible/              Bible loading, book names, reference parsing ("John 3:16")
    Services/           Song parser, slide builder, song library, importers, file storage
  Churchie.App/         The Avalonia desktop app
    Views/              Windows (.axaml = layout, .axaml.cs = code-behind)
    ViewModels/         What the windows show and do (MVVM)
    Controls/SlideView  Draws a slide; used for thumbnails, live view and the projector
    Bibles/kjv.json.gz  The bundled King James Version
tests/Churchie.Core.Tests   xUnit tests
.github/workflows/build.yml Builds, tests and packages for all platforms
.devcontainer/              Codespaces configuration
docs/                       Learning guide and roadmap
```

## Where your data lives

Songs, settings and the autosaved schedule are kept in:

- Windows: `%AppData%\Churchie`
- macOS: `~/Library/Application Support/Churchie`
- Linux: `~/.config/Churchie`

Use **File → Open data folder** to get there. Set the `CHURCHIE_DATA` environment variable to keep everything somewhere else (a USB stick, a synced folder).

### Adding Bible translations

Put Bible files in the `Bibles` folder inside the data folder (**Scripture tab → Open Bibles folder**) and press **Reload**. Supported:

- **Zefania XML** (`.xml`) — a free format with many translations, including African languages
- **Churchie JSON** (`.json` / `.json.gz`) — same format as the bundled KJV

Book names in the translation's own language work in the reference box (e.g. `Johanu 3:16`). Many modern translations are copyrighted, so only add ones your church is allowed to project.

## Building on a computer

Install the [.NET 10 SDK](https://dotnet.microsoft.com/download), then:

```bash
dotnet run --project src/Churchie.App    # start the app
dotnet test                              # run the tests
```

## Learn as you build

New to C#? **[docs/LEARNING-CSHARP.md](docs/LEARNING-CSHARP.md)** walks through the language using this project's own code, with small exercises. What's planned next is in **[docs/ROADMAP.md](docs/ROADMAP.md)**.

## Credits

The King James Version text comes from [scrollmapper/bible_databases](https://github.com/scrollmapper/bible_databases) (MIT licence) — see [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md). Built with [Avalonia UI](https://avaloniaui.net) and the [.NET Community Toolkit](https://github.com/CommunityToolkit/dotnet).
