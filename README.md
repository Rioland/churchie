<p align="center">
  <img src="src/Churchie.App/Assets/churchie.png" width="96" alt="Churchie icon" />
</p>

# Churchie

**Website: [churchie.netlify.app](https://churchie.netlify.app)** · [Download](https://churchie.netlify.app/download.html) · [How to use](https://churchie.netlify.app/docs.html)

Worship presentation software in the spirit of EasyWorship — song lyrics, scripture and pictures on the projector — written in **C#** with **Avalonia**, so the same app runs on **Windows, macOS and Linux**.

## What it does (version 0.2)

| Area | Features |
| --- | --- |
| **Layout** | Schedule (service order) · Preview · Live, with resource tabs underneath — the familiar EasyWorship arrangement |
| **Projector** | Full-screen output on the second screen (or a floating window when you only have one), smooth **cross-fade** between slides (cut / quick / normal / slow), **Black**, **Clear** (hide words, keep background) and **Logo** buttons |
| **Songs** | Library with search by title, words or author; editor that understands `[Verse 1]`, `[Chorus]`, `Bridge:`, `V2`…; import from `.txt` and OpenLyrics `.xml`; three public-domain hymns to start with |
| **Scripture** | Seven Bibles built in: KJV, World English Bible, Berean Standard Bible, ASV, Bible in Basic English, Young's Literal and the Yoruba Contemporary Bible. Type references the way people say them (`John 3:16-18`, `jn 3 16`, `1 Cor 13:4-7`, `Ps 23`, `Johanu 3:16`); browse by book and chapter; **Import Bible…** for other versions (Zefania, OpenSong or OSIS XML) |
| **Media** | Pictures as song or scripture backgrounds, as the logo, or shown full-screen; choose whether a picture fills the screen, shows whole, or stretches |
| **Themes** | Separate looks for songs and scripture: size, font, colours (with a colour picker), bold/shadow/uppercase, alignment, background picture with darkening — changes appear on screen instantly |
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

Get Churchie from **[churchie.netlify.app/download.html](https://churchie.netlify.app/download.html)**:

| Computer | Download | |
| --- | --- | --- |
| Windows 10/11 | `Churchie-Setup-win-x64.exe` | Installer: Next → Install → Finish, no admin password. Portable `Churchie-win-x64.zip` also available |
| Mac, Apple Silicon | `Churchie-osx-arm64.pkg` | Installer that puts Churchie.app in Applications (`.zip` of the app also available) |
| Mac, Intel | `Churchie-osx-x64.pkg` | The same for Intel Macs |
| Linux | `Churchie-linux-x64.tar.gz` | Extract and run `./Churchie/Churchie` |

> Churchie isn't code-signed yet. On Windows click **More info → Run anyway** the first time; on a Mac use **System Settings → Privacy & Security → Open Anyway**.

Every push to `main` also builds all of these on GitHub (**Actions** tab → latest **Build** run → **Artifacts**), and pushing a tag (`git tag v0.1.0 && git push --tags`) publishes them as a release.

## Work on it from anywhere (no PC needed)

The repository includes a **GitHub Codespaces** setup, so you can code in a browser — even on a tablet — and actually see the app running.

1. On github.com open this repo → green **Code** button → **Codespaces** → **Create codespace on main**.
2. Wait for VS Code to open in the browser (first time takes a few minutes; it installs .NET 10 and restores packages).
3. Open the **Ports** tab and click the globe next to **6080 – App desktop**. Password: `churchie`.
4. Back in VS Code: **Terminal → Run Task… → run app** (or press F5). Churchie appears in the desktop tab.

Small edits can also be made straight on github.com (press `.` on the repo page to open the web editor); every push triggers a new build.

## Website

The website lives in [`website/`](website/) (plain HTML/CSS/JS, no build step) and is hosted on Netlify (`netlify.toml` publishes that folder).

**Where the downloads come from.** Every download button links to `/get/<file name>` (for example `/get/Churchie-Setup-win-x64.exe`), using the file names produced by `build/package.sh`. One redirect rule in `netlify.toml` sends that on to the real file, so moving the files means changing only that rule. Right now the files are on the [`downloads`](https://github.com/Rioland/churchie/tree/downloads) branch of this project, which holds nothing but the installers. (The clones on the build machines fetch only `main`, so they don't download it.)

To publish a new version's downloads:

1. Build the packages: `bash build/package.sh <rid>` for `win-x64`, `linux-x64`, `osx-arm64` and `osx-x64`. The macOS ones must be built on a Mac, and the Windows installer needs [NSIS](https://nsis.sourceforge.io).
2. Replace the files on the `downloads` branch with everything from `dist/`.
3. Optionally create a GitHub release named `vX.Y.Z` and attach the same files. To serve the downloads from releases instead (GitHub's own download service, best for heavy use), change the rule in `netlify.toml` to `https://github.com/Rioland/churchie/releases/latest/download/:splat` and publish the website.

Once GitHub Actions is available, pushing a `vX.Y.Z` tag builds the files and makes the release automatically.

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

### Bible versions

Built in (free to use and share): **KJV**, **WEB** (World English Bible), **BSB** (Berean Standard Bible), **ASV**, **BBE** (Bible in Basic English), **YLT** (Young's Literal) and **OYCB** (Open Yoruba Contemporary Bible — *Bíbélì Mímọ́ Yorùbá Òde-Òní*). Each one's copyright line is shown under the Scripture tab.

To add another version, use **Scripture tab → Import Bible…** (or drop files into the `Bibles` folder and press **Reload**). Supported files:

- **Zefania XML**, **OpenSong XML** and **OSIS XML** (`.xml`) — the formats most Bible-software downloads use
- **Churchie JSON** (`.json` / `.json.gz`) — the format of the built-in Bibles

Book names in the translation's own language work in the reference box (e.g. `Johanu 3:16`, `Saamu 23`).

**NIV, Amplified, NKJV, ESV, NLT and most other modern versions are copyrighted.** Their publishers (e.g. Biblica for the NIV, The Lockman Foundation for the Amplified) license them separately, so Churchie can't include them. If your church has a licensed file of one of them, import it with **Import Bible…**.

## Building on a computer

Install the [.NET 10 SDK](https://dotnet.microsoft.com/download), then:

```bash
dotnet run --project src/Churchie.App    # start the app
dotnet test                              # run the tests
bash build/install-mac-app.sh            # Mac only: install ~/Applications/Churchie.app
```

## Learn as you build

New to C#? **[docs/LEARNING-CSHARP.md](docs/LEARNING-CSHARP.md)** walks through the language using this project's own code, with small exercises. What's planned next is in **[docs/ROADMAP.md](docs/ROADMAP.md)**.

## Credits

The King James Version text comes from [scrollmapper/bible_databases](https://github.com/scrollmapper/bible_databases) (MIT licence) — see [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md). Built with [Avalonia UI](https://avaloniaui.net) and the [.NET Community Toolkit](https://github.com/CommunityToolkit/dotnet).
