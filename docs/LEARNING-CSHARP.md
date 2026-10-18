# Learning C# with Churchie

You already write JavaScript/TypeScript, PHP, Java and Python, so you know how to program — this guide only covers what's *different* in C#, using code from this repository. Read it top to bottom once, then use it as a reference. Each section ends with a file to open.

---

## 1. The 2-minute map from what you know

| You know… | In C# it's… | Notes |
| --- | --- | --- |
| `npm` + `package.json` | **NuGet** + `.csproj` | Packages are listed as `<PackageReference>` in the `.csproj` |
| a monorepo | a **solution** (`Churchie.sln`) holding **projects** (`.csproj`) | Each project builds one `.dll` or app |
| TypeScript types | C# types — but enforced at runtime too | No `any`; `object` and `dynamic` exist but are rarely used |
| `string \| null` | `string?` | "Nullable reference types" are on, so the compiler warns when you might use null |
| Java class | C# class — almost identical | `extends`/`implements` are both written `:` |
| Java getters/setters | **properties**: `public string Title { get; set; }` | Use them like fields: `song.Title = "x"` |
| `Array.map/filter` | **LINQ**: `.Select()`, `.Where()` | Works on any list |
| `async/await` | `async Task` / `await` | Same idea; `Task` ≈ `Promise` |
| events / callbacks | `event Action?` and lambdas `x => …` | Lambdas look exactly like JS arrow functions |
| JSX / HTML | **XAML** (`.axaml` files) | Describes the UI; C# code-behind sits next to it |

Open: `src/Churchie.Core/Churchie.Core.csproj` and `Directory.Build.props` — notice how short they are.

---

## 2. Classes and properties

```csharp
// src/Churchie.Core/Models/Song.cs
public sealed class Song
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "";
    public List<SongSection> Sections { get; set; } = new();
}
```

- `public` / `private` work like Java. `sealed` means "nobody can inherit from this".
- `{ get; set; }` makes a **property**. The `= ""` part is its starting value.
- `new()` is short for `new List<SongSection>()` — C# can infer the type from the left side.
- Namespaces (`namespace Churchie.Core.Models;`) are like Java packages; `using` is like `import`.

**Computed property** (like a JS getter):

```csharp
public string AllText => string.Join("\n", Sections.Select(s => s.Text));
```

`=>` here means "this property returns…". `$"Hello {name}"` is string interpolation, like JS template literals.

Open: `src/Churchie.Core/Models/SlideTheme.cs`.

---

## 3. Records — classes for data snapshots

```csharp
// src/Churchie.Core/Models/Slide.cs
public sealed record Slide(SlideKind Kind, string Label, string Text, string? Footer = null, string? ImagePath = null);
```

One line creates a class with a constructor, read-only properties and value-based equality (two slides with the same text are "equal"). To copy with a change:

```csharp
var cleared = slide with { Text = "", Footer = null };   // used for the "Clear" button
```

This is like `{ ...slide, text: "" }` in JS.

---

## 4. Null safety

```csharp
public string? BackgroundImage { get; set; }    // may be null
public string BackgroundColor { get; set; } = "#000000";   // never null
```

Handy operators you'll see everywhere:

```csharp
var name = song?.Title;            // null if song is null (same as JS)
var title = name ?? "Untitled";    // fallback (same as JS)
if (SelectedSong is not { } song) return;   // "if there's no selected song, stop; otherwise call it song"
```

The last one is a **pattern** — C# can test and unpack in one go. Also common: `x is null`, `x is not null`, and `switch` expressions:

```csharp
var label = item.Kind switch
{
    ScheduleItemKind.Song => "SONG",
    ScheduleItemKind.Scripture => "BIBLE",
    _ => "IMAGE",          // _ = default
};
```

Open: `src/Churchie.App/ViewModels/ItemViewModels.cs`.

---

## 5. Lists and LINQ

```csharp
// src/Churchie.Core/Services/SongLibrary.cs
var byTitle = _songs.Where(s => s.Title.Contains(q, StringComparison.CurrentCultureIgnoreCase));
```

| JavaScript | C# LINQ |
| --- | --- |
| `arr.filter(x => …)` | `list.Where(x => …)` |
| `arr.map(x => …)` | `list.Select(x => …)` |
| `arr.find(x => …)` | `list.FirstOrDefault(x => …)` |
| `arr.some(x => …)` | `list.Any(x => …)` |
| `arr.length` | `list.Count` (lists) / `.Count()` (anything) |

LINQ is *lazy*: nothing runs until you loop over it or call `.ToList()`.

---

## 6. async / await

```csharp
// src/Churchie.App/ViewModels/MainWindowViewModel.Songs.cs
private async Task ImportSongsAsync()
{
    var files = await Dialogs.PickFilesAsync("Import songs", …);
    …
}
```

Same as JS: `async` methods return a `Task` (a promise), `await` waits without freezing the window. `Task.Run(() => …)` moves heavy work (like loading a Bible) off the UI thread — see `LoadBiblesAsync` in `MainWindowViewModel.cs`.

---

## 7. How the app is wired: MVVM

The UI follows **Model – View – ViewModel**:

- **Model** — plain data and logic (`Churchie.Core`): songs, slides, Bibles. No UI code at all, which is why it can be unit-tested.
- **ViewModel** — what a window shows and the actions it can take (`ViewModels/`).
- **View** — the layout in XAML (`Views/*.axaml`). It *binds* to the view model.

```xml
<!-- Views/MainWindow.axaml -->
<TextBox Text="{Binding SongSearch}" />
<Button Content="Add to schedule" Command="{Binding AddSongToScheduleCommand}" />
```

```csharp
// ViewModels/MainWindowViewModel.Songs.cs
[ObservableProperty] private string _songSearch = "";
partial void OnSongSearchChanged(string value) => RefreshSongList();

[RelayCommand]
private void AddSongToSchedule() { … }
```

What's happening:

- `[ObservableProperty]` is an **attribute** (like a Java annotation / TS decorator). The *CommunityToolkit.Mvvm* package reads it at compile time and writes the `SongSearch` property for you, including the "I changed!" notification the UI listens to.
- Typing in the box updates `SongSearch`, which calls `OnSongSearchChanged`, which refreshes the list — no manual event wiring.
- `[RelayCommand]` turns the method into `AddSongToScheduleCommand`, which the button runs.
- `partial class` lets one class be split across files — that's how the generator adds code, and how `MainWindowViewModel` is split into Songs / Scripture / Media / Settings files.

`x:DataType="vm:MainWindowViewModel"` at the top of the XAML tells the compiler what the bindings refer to, so a typo in `{Binding SongSerch}` is a build error rather than a silent bug.

---

## 8. Drawing a slide

`src/Churchie.App/Controls/SlideView.cs` is a custom control. It lays every slide out on a fixed **1920×1080** canvas and wraps it in a `Viewbox`, which scales it to whatever size it's given — so the thumbnail, the live preview and the projector are pixel-for-pixel the same picture. The text sits in a second `Viewbox` set to `StretchDirection.DownOnly`, which shrinks long passages to fit but never enlarges short ones.

Its two inputs are **styled properties** (`Slide` and `Appearance`) — Avalonia's version of bindable properties. When either changes, `OnPropertyChanged` calls `Refresh()`.

---

## 9. Tests

```csharp
// tests/Churchie.Core.Tests/ScriptureTests.cs
[Theory]
[InlineData("jn 3 16", 43, 3, 16, null)]
public void Parses_common_references(string input, int book, int chapter, int? start, int? end) { … }
```

`[Fact]` is one test; `[Theory]` + `[InlineData]` runs the same test with different inputs (like `test.each` in Jest). Run them with `dotnet test`, or let GitHub Actions run them on every push.

---

## 10. Exercises (in order of difficulty)

1. **Change a default.** Make new song slides use a font size of 90: edit `FontSize` in `SlideTheme.cs`. (Only affects fresh installs — existing settings are saved in `settings.json`.)
2. **Add a sample hymn.** Add "What a Friend We Have in Jesus" (public domain) to `SampleSongs.cs`, then add an assertion to `LibraryTests.First_run_adds_sample_hymns_and_reloads_them` (it expects 3 songs — update it).
3. **Teach the parser a new label.** Make `Tag` lines show as "Tag" *and* support `Ending 2`. Look at the `WordHeader` regex in `SongParser.cs` and add a test in `SongParserTests.cs` first — then make it pass.
4. **Show copyright on the last slide.** In `SlideBuilder.FromSong`, give the final slide a `Footer` with `song.Copyright`. Hint: `slides[^1]` is the last item, and records can be copied with `with { … }`.
5. **New setting.** Add "Show song title on first slide" to `AppSettings`, a `CheckBox` in the Themes & Settings tab, and use it in `SlideFactory`. You'll touch Core (model + slide building), the view model (`[ObservableProperty]` + `partial void On…Changed`) and the XAML — the full MVVM loop.

---

## Glossary

- **.NET** — the runtime and libraries (like Node for JS). We use .NET 10.
- **Avalonia** — the cross-platform UI framework (think React Native, but for desktop, with XAML instead of JSX).
- **Assembly** — a compiled `.dll` or `.exe`.
- **Generic** — `List<Song>` means "a list of songs" (same as TypeScript `Array<Song>`).
- **Interface** — `IDialogService`; a contract a class promises to fulfil (same as Java/TS).
- **Source generator** — a compiler plug-in that writes code for you (CommunityToolkit.Mvvm, Avalonia's XAML compiler).
