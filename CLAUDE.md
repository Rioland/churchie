# Notes for AI assistants working on Churchie

- Stack: .NET 10, C# (latest), Avalonia **11.3.x** (not 12 — APIs differ), CommunityToolkit.Mvvm 8.4 with field-style `[ObservableProperty]`.
- Projects: `src/Churchie.Core` (no dependencies, all logic, unit-tested), `src/Churchie.App` (Avalonia UI, MVVM), `tests/Churchie.Core.Tests` (xUnit).
- Compiled bindings are on by default: every XAML file and `DataTemplate` needs `x:DataType`; bind to instance properties, not statics.
- Keep logic in Core with tests; view models stay thin; code-behind only for window-specific things (keyboard, double-click, opening the output window).
- The owner is learning C# — keep code readable, comment the "why", and prefer plain constructs over clever ones. Explain changes in plain language.
- Build/test: `dotnet build Churchie.sln`, `dotnet test`. GitHub Actions (`.github/workflows/build.yml`) builds and packages Windows, macOS (arm64 + x64) and Linux on every push to `main`.
- Staged roadmap is in `docs/ROADMAP.md`; update it when a phase item lands.
