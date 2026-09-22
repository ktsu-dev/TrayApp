# ktsu.TrayApp

> The reusable skeleton behind cross-platform .NET tray tools: you bring a menu and a platform layer, it brings the tray.

[![License](https://img.shields.io/github/license/ktsu-dev/TrayApp.svg?label=License&logo=nuget)](LICENSE.md)
[![NuGet Version](https://img.shields.io/nuget/v/ktsu.TrayApp?label=Stable&logo=nuget)](https://nuget.org/packages/ktsu.TrayApp)
[![NuGet Version](https://img.shields.io/nuget/vpre/ktsu.TrayApp?label=Latest&logo=nuget)](https://nuget.org/packages/ktsu.TrayApp)
[![NuGet Downloads](https://img.shields.io/nuget/dt/ktsu.TrayApp?label=Downloads&logo=nuget)](https://nuget.org/packages/ktsu.TrayApp)
[![GitHub commit activity](https://img.shields.io/github/commit-activity/m/ktsu-dev/TrayApp?label=Commits&logo=github)](https://github.com/ktsu-dev/TrayApp/commits/main)
[![GitHub contributors](https://img.shields.io/github/contributors/ktsu-dev/TrayApp?label=Contributors&logo=github)](https://github.com/ktsu-dev/TrayApp/graphs/contributors)
[![GitHub Actions Workflow Status](https://img.shields.io/github/actions/workflow/status/ktsu-dev/TrayApp/dotnet.yml?label=Build&logo=github)](https://github.com/ktsu-dev/TrayApp/actions)

## Introduction

`ktsu.TrayApp` is what is left of a working tray tool once the part that is specific to that tool is
taken out. A tool built on it supplies a menu model and its own platform layer; the library owns the
Avalonia tray host for Windows, macOS and Linux, the decision between a tray icon and a terminal,
Ctrl+C and `SIGTERM`, the `--for` duration timer, the standard command line flags, the `--status`
report, and debounced preference persistence.

Most of that is not hard so much as easy to get subtly wrong, and every one of those mistakes ends the
same way: a process that exits when the user asked it to keep running, or keeps running after the user
asked it to stop. The behaviours that cost the most to rediscover are listed under
[Load-bearing behaviour](#load-bearing-behaviour), with what breaks if each one is removed.

## Features

- **Fluent entry point**: `TrayAppBuilder` describes a tool - its menu, icons, flags, and lifecycle -
  and runs it.
- **Framework-free menu model**: toggles, commands, separators and a disabled status line, each reading
  its state through a getter, so one `Refresh()` re-reads everything. Quit is appended automatically.
- **Tray or terminal, never neither**: the tray is used wherever there is a session to put it in, and a
  windowing stack that refuses at the last moment falls back to the console rather than killing the
  process.
- **Signals handled**: Ctrl+C and `SIGTERM` both release through the tool's own stop action, rather than
  leaving cleanup to the operating system.
- **Standard flags**: `--tray`, `--no-tray`, `--for <duration>`, `--status`, `--help`, `--version`, plus
  whatever flags the tool registers, all in one generated usage block.
- **Duration parsing that matches what people type**: `45s`, `90m`, `2h`, `1h30m`, and a bare `90` for
  ninety minutes.
- **Optional persistence**: remembered toggle states over any `ktsu.Essentials`
  `IPersistenceProvider<string>`, written in coalesced bursts. A tool that persists nothing does no I/O
  and takes no provider dependency.
- **Packaging props in the box**: `build/ktsu.TrayApp.props` trims the per-RID SkiaSharp natives and
  debug symbols Avalonia drags into a RID-agnostic tool package, from ~181 MiB to ~48 MiB.

## Installation

### Package Manager Console

```powershell
Install-Package ktsu.TrayApp
```

### .NET CLI

```bash
dotnet add package ktsu.TrayApp
```

### Package Reference

```xml
<PackageReference Include="ktsu.TrayApp" Version="x.y.z" />
```

## Usage Examples

### Basic Example

```csharp
using ktsu.TrayApp;

internal static class Program
{
	private static async Task<int> Main(string[] args)
	{
		Stopwatch stopwatch = new();

		return await TrayAppBuilder.Create("trayapp-demo")
			.DisplayName("TrayApp Demo")
			.Summary("a minimal tray tool built on ktsu.TrayApp.")
			.Icons(typeof(Program).Assembly, "Assets.tray-active.png", "Assets.tray-idle.png")
			.Status(stopwatch.Describe)
			.Toggle("Running", () => stopwatch.IsRunning, stopwatch.SetRunning, persistAs: "running")
			.OnStart(stopwatch.Begin)
			.OnStop(stopwatch.Stop)
			.RunAsync(args)
			.ConfigureAwait(false);
	}
}
```

That is the whole of `examples/TrayApp.Demo`. It gets a tray icon with a status line, a checkable
entry, and a quit entry; a console mode for machines with no desktop; `--help`, `--version`,
`--status`, and `--for 90m`; and Ctrl+C and `SIGTERM` both releasing through `OnStop`.

### Embedding the tray images

The images are looked up by resource name, so they have to be embedded:

```xml
<ItemGroup>
  <EmbeddedResource Include="Assets/*.png" />
</ItemGroup>
```

`Assets.tray-active.png` is resolved against the assembly name, so
`ktsu.Example.Assets.tray-active.png` is found without spelling it out. When a name misses, the
exception lists every resource the assembly does have - which is the difference between a one-line fix
and an afternoon.

`scripts/generate-icons.py` draws a pair, parameterised so a consuming repository can use it as is:

```bash
python3 scripts/generate-icons.py --tray-dir MyTool/Assets --package-icon icon.png --glyph square
```

### Persisting what the user set

The library depends on `ktsu.Essentials` for the abstraction only. The tool picks where its settings
live and hands the result in, so nothing here forces a storage backend, a serializer, or a file system
on a tool that persists nothing:

```csharp
using ktsu.Essentials;
using ktsu.Essentials.FileSystemProviders.Native;
using ktsu.Essentials.PersistenceProviders.ConfigHome;
using ktsu.Essentials.SerializationProviders.Json;

IPersistenceProvider<string> preferences = new ConfigHomePersistenceProvider<string>(
	new NativeFileSystemProvider(),
	new JsonSerializationProvider(),
	"trayapp-demo",
	string.Empty);

// ...
	.Toggle("Running", () => stopwatch.IsRunning, stopwatch.SetRunning, persistAs: "running")
	.Preferences(preferences)
```

with the matching package references in the tool, not in the library:

```xml
<PackageReference Include="ktsu.Essentials" />
<PackageReference Include="ktsu.Essentials.FileSystemProviders.Native" />
<PackageReference Include="ktsu.Essentials.PersistenceProviders.ConfigHome" />
<PackageReference Include="ktsu.Essentials.SerializationProviders.Json" />
```

Only toggles given a `persistAs` key are remembered, and only the tray reads them: a command line run
says what it wants, so letting a remembered setting override an argument would be surprising. Within
the tray, what was typed this time beats what the tray was left set to last time.

`IPersistenceProvider` is asynchronous and a menu click is not, so the value is held in memory, a
change restarts a short timer, and the write happens once the clicking stops - or at the latest on the
way out. A user flicking a toggle back and forth produces one write, not one per flick.

### Application-specific flags

```csharp
	.Flag(["-d", "--display"], "Keep the display lit as well.", () => keepDisplayAwake = true)
	.Option(["-r", "--reason"], "text", "Reason recorded with the inhibitor.", value => reason = value)
```

They appear in `--help` in registration order, between the tray switches and `--for`. A value handler
that throws `FormatException` or `ArgumentException` turns its message into the usage error, and the
tool exits 2 without starting.

### Adding rows to `--status`

```csharp
	.Status(() => controller.Describe())
	.Status("Mechanism", () => controller.Mechanism)
```

```text
nosleep 1.2.0
Platform:      Ubuntu 24.04.4 LTS (ubuntu.24.04-x64)
Tray icon:     available (DISPLAY or WAYLAND_DISPLAY is set)
State:         Keeping system awake
Mechanism:     systemd-inhibit
```

## Advanced Usage

### Telling the tray that something changed

The menu only ever reads through getters, so a tool whose state changes without a click just needs to
nudge it:

```csharp
	.RefreshOn(refresh => controller.StateChanged += (_, _) => refresh())
```

The delegate is safe to call from any thread; it posts to the UI thread itself.

### Choosing the icon

Without `ActiveWhen`, the first registered toggle decides which of the two images is shown. A tool
whose tray state is not its first toggle says so:

```csharp
	.ActiveWhen(() => controller.IsActive)
```

### Packaging

`build/ktsu.TrayApp.props` and its companion `.targets` are imported into any project that references
the package. A `DotnetTool` package is RID-agnostic, so a tool that links Avalonia otherwise ships
SkiaSharp's and HarfBuzzSharp's native assets for every RID those packages support - Android bionic,
loongarch, riscv and the rest - plus a native `.pdb` for each one. Measured on a minimal tool:
**180.9 MiB** without the trimming, **47.6 MiB** with it.

Restore the full set:

```xml
<PropertyGroup>
  <TrayAppTrimToolRuntimeAssets>false</TrayAppTrimToolRuntimeAssets>
</PropertyGroup>
```

or keep the trimming and add one runtime back:

```xml
<PropertyGroup>
  <TrayAppToolRuntimeIdentifiers>;win-x64;win-x86;win-arm64;osx;osx-x64;osx-arm64;linux-x64;linux-arm64;linux-arm;linux-musl-x64;</TrayAppToolRuntimeIdentifiers>
</PropertyGroup>
```

The leading and trailing semicolons matter: they let the match be on a whole entry rather than a
prefix.

## Load-bearing behaviour

These are the parts that look like they could be simplified away and cannot.

| Behaviour | What breaks without it |
|-----------|------------------------|
| Catching **all** exceptions out of the Avalonia run call, and using `HasStarted` to tell "the tray ran and quit" from "the tray never came up" | Avalonia's `DBusTrayIconImpl.WatchAsync` is `async void` with an exception filter that stops applying once the icon is disposed, so an ordinary Linux shutdown throws `TaskCanceledException` *after* the tray has done its job. Without the flag, a clean quit restarts in the console. |
| Falling back to the console when the windowing stack refuses after `DesktopSession` said yes | A `DISPLAY` pointing at nothing, a missing libX11, or no status-notifier host kills a process someone started to hold a resource. |
| Constructing menu items eagerly and the `TrayIcon` lazily | `TrayIcon`'s constructor reaches for a platform handle that does not exist until Avalonia has started. |
| Keeping a failed action's message in a field rather than writing it into the menu item | The refresh that runs immediately after every action overwrites anything written directly. |
| `TrayIcon.SetIcons(app, [icon])` | That call is what disposes the icon on shutdown; a bare `TrayIcon` outlives the process on some Linux panels. |
| `MacOSPlatformOptions { ShowInDock = false }`, with a direct `Avalonia.Native` package reference | A tray-only app otherwise takes a dock slot; analyzer KTSU0006 rejects using a transitive package's types. |
| Not awaiting anything before the tray starts | Avalonia's macOS backend has to run on the process's main thread, and the continuation after an `await` in a console application runs on a thread pool thread. |
| A bare number in `--for` meaning minutes, and only as the whole value | `--for 90` is what people type; `5m5` is a typo, not ten minutes. |

## API Reference

### `TrayAppBuilder`

Describes a cross-platform tray tool and runs it. Named `TrayAppBuilder` rather than `TrayApp` because
a type whose name matches the last segment of its own namespace makes every `TrayApp.Something`
reference ambiguous.

#### Methods

| Name | Return Type | Description |
|------|-------------|-------------|
| `Create(string name)` | `TrayAppBuilder` | Starts describing a tool with the command name as typed. |
| `DisplayName(string value)` | `TrayAppBuilder` | The name shown in the menu and in messages. Defaults to the command name. |
| `Summary(string value)` | `TrayAppBuilder` | The sentence printed under the usage line. |
| `Status(Func<string> describe)` | `TrayAppBuilder` | What the tool says it is doing: the menu's status line, the tooltip, and the `State` row of `--status`. |
| `Status(string label, Func<string> value)` | `TrayAppBuilder` | An extra label/value row in the `--status` report. |
| `Icons(Assembly assembly, string active, string idle)` | `TrayAppBuilder` | The two tray images, by embedded resource name. |
| `Icon(Assembly assembly, string resourceName)` | `TrayAppBuilder` | One tray image, used whatever the state. |
| `ActiveWhen(Func<bool> predicate)` | `TrayAppBuilder` | Which image is shown. Defaults to the first toggle. |
| `Toggle(string label, Func<bool> get, Action<bool> set, Func<bool>? isEnabled, string? persistAs)` | `TrayAppBuilder` | A checkable menu entry, optionally remembered between runs. |
| `Command(string label, Action action, Func<bool>? isEnabled)` | `TrayAppBuilder` | A menu entry that runs an action. |
| `Separator()` | `TrayAppBuilder` | A horizontal rule between groups of entries. |
| `Flag(IReadOnlyList<string> aliases, string description, Action onSet)` | `TrayAppBuilder` | A command line switch that takes no value. |
| `Option(IReadOnlyList<string> aliases, string valueName, string description, Action<string> onValue)` | `TrayAppBuilder` | A command line option that consumes the argument after it. |
| `OnStart(Action action)` | `TrayAppBuilder` | What to run when the tool starts, in both modes. |
| `OnStop(Action action)` | `TrayAppBuilder` | What to run on the way out, in both modes. |
| `RefreshOn(Action<Action> subscribe)` | `TrayAppBuilder` | Hands the tool a delegate that refreshes the menu. |
| `Preferences(IPersistenceProvider<string> provider, string key, TimeSpan? debounce)` | `TrayAppBuilder` | Remembers persisted toggles between runs. |
| `RunAsync(string[] args)` | `Task<int>` | Parses the command line and runs the tool. 0 success, 1 the platform refused, 2 the command line was wrong. |

### `TrayMenu`

The whole menu: the tool's entries, a status line above them, a separator and quit below. Free of any
windowing toolkit, so a menu's behaviour can be asserted on a machine with no display.

#### Properties

| Name | Type | Description |
|------|------|-------------|
| `Items` | `IReadOnlyList<TrayMenuItem>` | Every entry, including the status line, the separators, and quit. |
| `StatusItem` | `TrayStatusItem?` | The disabled line at the top, or `null`. |
| `QuitItem` | `TrayCommandItem` | The quit entry. |
| `LastError` | `string?` | The message from the last action that failed. |

#### Methods

| Name | Return Type | Description |
|------|-------------|-------------|
| `Refresh()` | `void` | Re-reads every item's state from the tool. |
| `Activate(TrayMenuItem item)` | `bool` | Clicks an item, records any failure, and refreshes. |
| `Run(Action action)` | `bool` | Runs one of the tool's actions under the same failure handling as a click. |

#### Events

| Name | Description |
|------|-------------|
| `QuitRequested` | Raised when the quit entry is clicked. The menu knows nothing about how the process ends. |

### `TrayMenuItem` and its kinds

| Type | Description |
|------|-------------|
| `TrayMenuItem` | Base: `Header`, `IsEnabled`, and `Refresh()`. |
| `TrayToggleItem` | Checkable. `IsChecked`, `PreferenceKey`, `Toggle()`, `Set(bool)`. |
| `TrayCommandItem` | Runs an action. `Invoke()`. |
| `TraySeparatorItem` | A horizontal rule. |
| `TrayStatusItem` | The disabled line that carries the state, or the last failure. |

### `TrayIconSet`

The tool's tray images, loaded once from its assembly's embedded resources and deferred until Avalonia
has started.

| Name | Return Type | Description |
|------|-------------|-------------|
| `FromResources(Assembly, string active, string idle)` | `TrayIconSet` | Loads a pair, by full or assembly-relative resource name. |
| `FromResource(Assembly, string resourceName)` | `TrayIconSet` | Loads one image, used for both states. |
| `For(bool isActive)` | `WindowIcon` | The image for a state. |

### `DesktopSession`

| Name | Type | Description |
|------|------|-------------|
| `IsAvailable` | `bool` | Whether a tray icon can be shown. |
| `Explanation` | `string` | A sentence explaining `IsAvailable`, for `--status` and the fallback notice. |

### `StatusReport`

| Name | Return Type | Description |
|------|-------------|-------------|
| `Build(string name, string? status, IReadOnlyList<StatusDetail> details)` | `string` | The multi-line text behind `--status`. |

### `Cli.DurationParser`

| Name | Return Type | Description |
|------|-------------|-------------|
| `TryParse(string? text, out TimeSpan duration)` | `bool` | Reads `45s`, `90m`, `2h`, `1h30m`, or a bare `90` for minutes. |

### `Cli.CommandLineParser`

| Name | Return Type | Description |
|------|-------------|-------------|
| `TryParse(IReadOnlyList<string> args, IReadOnlyList<AppOption> appOptions, out CommandLineOptions options, out string error)` | `bool` | Parses the standard flags plus a tool's own. Matches are recorded, not applied. |

### `Cli.HelpText`

| Name | Return Type | Description |
|------|-------------|-------------|
| `Usage(string name, string? summary, IReadOnlyList<AppOption> appOptions)` | `string` | The generated usage block. |
| `Version(string name, Assembly? assembly)` | `string` | The version line, from the entry assembly by default. |

## Contributing

Contributions are welcome! Feel free to open issues or submit pull requests.

## License

This project is licensed under the MIT License. See the [LICENSE.md](LICENSE.md) file for details.
