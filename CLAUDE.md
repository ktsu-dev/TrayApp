# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build Commands

```bash
# Restore, build, and test (standard workflow)
dotnet restore
dotnet build
dotnet test

# Run a single test
dotnet test --filter "FullyQualifiedName~TestMethodName"

# Build specific configuration
dotnet build -c Release

# Run the demo tool
dotnet run --project examples/TrayApp.Demo -- --status

# Pack the library and inspect the payload
dotnet pack TrayApp/TrayApp.csproj -c Release -o ./artifacts
unzip -l artifacts/ktsu.TrayApp.*.nupkg
```

If `dotnet test` reports "Zero tests ran", run the test host directly - some SDK builds do not drive
Microsoft Testing Platform correctly through `dotnet test`:

```bash
./TrayApp.Test/bin/Debug/net10.0/ktsu.TrayApp.Test
```

## Project Structure

This is a .NET library (`ktsu.TrayApp`) that a cross-platform tray tool is built on: the tool brings a
menu model and its own platform layer, and the library brings everything else. The solution uses:

- **ktsu.Sdk** - Custom SDK providing shared build configuration
- **ktsu.Sdk.ConsoleApp** - Used by the demo under `examples/`
- **MSTest.Sdk** - Test project SDK with Microsoft Testing Platform
- Multi-targeting: `net10.0;net9.0` for the library, `net10.0` for the tests and the demo

### Key Files

- `TrayApp/TrayAppBuilder.cs` - The fluent entry point, and the run: the tray/console decision, the
  fallback, the preference restore, and the order the command line is applied in
- `TrayApp/TrayAppDefinition.cs` - What the builder freezes; internal, so the runner's shape is not an
  API decision
- `TrayApp/Menu/TrayMenu.cs` - The menu: the tool's items, a status line, a separator and quit. Owns
  `LastError` and the failure handling around a click
- `TrayApp/Menu/Tray*Item.cs` - Toggle, command, separator, and the disabled status line. Every visible
  property comes from a getter
- `TrayApp/Tray/TrayApplication.cs` - The Avalonia host: projects the menu model onto native menu items
  and owns `HasStarted`
- `TrayApp/ConsoleRunner.cs` - The headless mode, including Ctrl+C and `SIGTERM`
- `TrayApp/TrayIconSet.cs` - Loads the tray PNGs by embedded resource name, and says what the assembly
  does have when a name misses
- `TrayApp/DesktopSession.cs` - Whether this process has a desktop session to put a tray icon in
- `TrayApp/StatusReport.cs` - The text behind `--status`
- `TrayApp/Cli/` - Hand-written argument parsing, duration parsing, and the generated usage text
- `TrayApp/Preferences/DebouncedPreferenceStore.cs` - Synchronous reads and coalesced writes over an
  asynchronous `IPersistenceProvider`
- `TrayApp/msbuild/ktsu.TrayApp.props` + `.targets` - Shipped in the package as `build/`; trim the tool payload
- `examples/TrayApp.Demo/` - A tray tool with one toggle, and nothing else
- `scripts/generate-icons.py` - Draws a tray icon pair and a package icon, parameterised
- `scripts/status-notifier-watcher.py` - A minimal StatusNotifierWatcher, for exercising the Linux tray
  without a panel

### Dependencies

- **Avalonia / Avalonia.Desktop / Avalonia.Native** - The only cross-platform tray icon API that covers
  `Shell_NotifyIcon`, `NSStatusItem`, and StatusNotifierItem from one codebase. `Avalonia.Native` is
  referenced directly for `MacOSPlatformOptions`, which keeps a tray-only app out of the macOS dock
- **ktsu.Essentials** - For `IPersistenceProvider<string>` and nothing else. The concrete provider is
  the consuming tool's choice and is handed in through `Preferences`, so no storage backend,
  serializer, or file system is forced on a tool that persists nothing
- **Polyfill** - Required by KTSU0001 for non-test projects; also supplies `Ensure.NotNull`

## Architecture

The split is: a tool describes itself to `TrayAppBuilder` and the builder runs it. Nothing in the menu
model references Avalonia, so `TrayMenu` and its items can be asserted on a build agent with no
display; `Tray/TrayApplication` is the only file that knows what a `NativeMenuItem` is.

```csharp
return await TrayAppBuilder.Create("nosleep")
    .Icons(typeof(Program).Assembly, "Assets.tray-active.png", "Assets.tray-idle.png")
    .Status(() => controller.Describe())
    .Toggle("Keep awake", () => controller.IsActive, _ => controller.Toggle())
    .Preferences(persistenceProvider)
    .RunAsync(args);
```

The type is `TrayAppBuilder` rather than `TrayApp` because a type whose name matches the last segment of
its own namespace makes every `TrayApp.Something` reference ambiguous - which is the reason CA1724 is
suppressed across ktsu.Sdk, and not a reason to spend the name here.

A run goes: parse, then `--help`/`--version`, then restore preferences (tray only), then apply the
tool's own flags, then `--status`, then the run. The order of the middle two is the point: a remembered
setting is restored first so that what was typed this time beats what the tray was left set to. The
parser records matches rather than invoking handlers, which is what makes that order possible.

### Load-bearing behaviour

Easy to undo by accident, and each one has been paid for once:

- **Avalonia's Linux tray teardown throws.** `DBusTrayIconImpl.WatchAsync` is `async void` with an
  exception filter that stops applying once the icon is disposed, so an ordinary shutdown throws
  `TaskCanceledException` out of `StartWithClassicDesktopLifetime` *after* the tray has done its job.
  `TrayApplication.HasStarted`, set at the end of `OnFrameworkInitializationCompleted`, is how
  `TrayAppBuilder.RunTray` tells that apart from a tray that never came up. Without it, a clean quit
  restarts in the console.
- **Fall back, never die.** If the windowing stack refuses after `DesktopSession` said yes, the run goes
  to the console. Those failures arrive as plain exceptions of whatever type the backend felt like
  (X11 throws `Exception("XOpenDisplay failed")`), so the catch is unfiltered on purpose.
- **Menu items eagerly, the `TrayIcon` lazily.** `TrayIcon`'s constructor reaches for a platform handle
  that does not exist until Avalonia has started; `NativeMenuItem` does not.
- **`LastError` is a field on `TrayMenu`.** The refresh that runs right after every action would
  overwrite a message written straight into the status item.
- **`TrayIcon.SetIcons(app, [icon])`** is what disposes the icon on shutdown; a bare `TrayIcon` outlives
  the process on some Linux panels.
- **Nothing is awaited before the tray starts.** Avalonia's macOS backend has to run on the process's
  main thread, and the continuation after an `await` in a console application runs on a thread pool
  thread. `LoadPreferences` blocks for that reason, which is safe because a console application has no
  synchronization context.
- **A bare number in `--for` means minutes, and only as the whole value.** `--for 90` is the shape
  people reach for; `5m5` is a typo, not ten minutes.

## Testing

MSTest with Microsoft Testing Platform. `[assembly: InternalsVisibleTo("ktsu.TrayApp.Test")]` is what
lets the tests build menus from the item constructors directly and drive
`TrayAppBuilder.LoadPreferences` and `TryApplyOptions`, which is where the ordering rule above lives.

`DebouncedPreferenceStoreTests` runs against `ktsu.Essentials.PersistenceProviders.InMemory`, so the
round trip is the real one with no directory to clean up. `CountingPersistenceProvider` wraps it,
because "wrote the right value in the end" does not distinguish a store that coalesces from one that
writes on every click.

Assertions about a menu model are no substitute for showing a tray icon. On Linux that needs a display
*and* a status-notifier host on the session bus:

```bash
pip install dbus-next
eval "$(dbus-launch --sh-syntax)"
python3 scripts/status-notifier-watcher.py &
xvfb-run -a env DBUS_SESSION_BUS_ADDRESS="$DBUS_SESSION_BUS_ADDRESS" \
  dotnet run --project examples/TrayApp.Demo -- --tray --for 5s
```

Success is the watcher logging a registered `org.kde.StatusNotifierItem-...`, the run lasting the full
five seconds, and an exit code of 0. An early or non-zero exit means the teardown handling is wrong.
The fallback is the same command with no `DISPLAY`: it must print the "staying in the terminal" notice
and still run.

## Packaging

`TrayApp/msbuild/ktsu.TrayApp.props` and `TrayApp/msbuild/ktsu.TrayApp.targets` are packed to `build/`
inside the package and are imported into any project that references it. A `DotnetTool` package is RID-agnostic, so without
them a tool that links Avalonia ships SkiaSharp's and HarfBuzzSharp's native assets for every RID those
packages support, plus a native `.pdb` for each - 180.9 MiB against 47.6 MiB, measured on a minimal
tool.

There are two files rather than one because NuGet imports `build/*.props` before the referencing
project's own SDK props, and ktsu.Sdk sets `CopyDebugSymbolFilesFromPackages` unconditionally there.
`build/*.targets` is imported after both, which is late enough to win and still early enough that a
consumer's own value, set in its project body, is visible in the condition.

`TrayAppTrimToolRuntimeAssets=false` restores everything; `TrayAppToolRuntimeIdentifiers` adds a RID
back without giving up the rest.

The source folder is `msbuild/` rather than `build/`, and the `build/` layout NuGet requires is applied
by `PackagePath` instead. The canonical `.gitignore` ktsu.Sdk syncs (see below) ignores `**/build/`, and
an exception added to it does not survive: a file added under `TrayApp/build/` is silently dropped from
the next commit, and the package ships without the props that are the point of it.

`TrayApp/CompatibilitySuppressions.xml` covers the compiler shims Polyfill injects into the net9.0
build and not the net10.0 one. Every entry is under `System`; if a regeneration adds one that is not,
that is a real API break between the two target frameworks.

## What the build rewrites underneath you

Two files in this repository are regenerated during a build rather than being the source of truth, and
both bit once already:

- **ktsu.Sdk syncs its own style files over yours.** `_KtsuSyncStyleConfigFiles` runs before
  `PrepareForBuild` and copies the SDK package's canonical `editorconfig`, `gitattributes`, `gitignore`
  and `runsettings` over the repository's, silently. Edits to those four do not survive a build unless
  they also land in ktsu.Sdk. Opt out with `<KtsuSyncStyleConfigFiles>false</KtsuSyncStyleConfigFiles>`
  if you ever genuinely need to.
- **The file header comes from `COPYRIGHT.md`.** That sync also rewrites `.editorconfig`'s
  `file_header_template` from `$(Copyright)`, which ktsu.Sdk reads out of `COPYRIGHT.md` - and on CI
  `ktsubuild ci` regenerates `COPYRIGHT.md` first, as `Copyright (c) 2023-<year> ktsu-dev contributors`,
  for repositories under the `ktsu-dev` owner. A header copied from a repository under a different
  owner, where KtsuBuild skips that generation, builds locally and then fails IDE0073 on every file in
  CI. The header, `COPYRIGHT.md` and `file_header_template` have to say the same thing, and that thing
  is what KtsuBuild generates.

## CI/CD

Uses the KtsuBuild tool (`ktsu.KtsuBuild.Tool`) for the CI pipeline. Version increments are controlled
by commit message tags: `[major]`, `[minor]`, `[patch]`, `[pre]`.

## Code Quality

Do not add global suppressions for warnings. Use explicit suppression attributes with justifications
when needed, with preprocessor defines only as fallback. Make the smallest, most targeted suppressions
possible.

Auto-generated files (`VERSION.md`, `CHANGELOG.md`, `LICENSE.md`) should never be edited manually.
