// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.TrayApp;

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia;
using ktsu.Essentials;
using ktsu.TrayApp.Cli;
using ktsu.TrayApp.Menu;
using ktsu.TrayApp.Preferences;
using ktsu.TrayApp.Tray;

/// <summary>
/// Describes a cross-platform tray tool and runs it.
/// </summary>
/// <remarks>
/// <para>
/// A tool supplies a menu and its own platform layer; everything else - the Avalonia tray host, the
/// tray-versus-console decision and its fallback, Ctrl+C and <c>SIGTERM</c>, the <c>--for</c> timer, the
/// standard flags, the <c>--status</c> report, and debounced preference persistence - lives here.
/// </para>
/// <example>
/// <code>
/// return await TrayAppBuilder.Create("nosleep")
///     .Icons(typeof(Program).Assembly, "Assets.tray-active.png", "Assets.tray-idle.png")
///     .Status(() =&gt; controller.Describe())
///     .Toggle("Keep awake", () =&gt; controller.IsActive, _ =&gt; controller.Toggle())
///     .Toggle("Keep display awake too", () =&gt; controller.KeepDisplayAwake, controller.SetKeepDisplayAwake)
///     .Preferences(persistenceProvider)
///     .RunAsync(args);
/// </code>
/// </example>
/// <para>
/// The type is <c>TrayAppBuilder</c> rather than <c>TrayApp</c> because a type whose name matches the last
/// segment of its own namespace makes every <c>TrayApp.Something</c> reference ambiguous - which is the
/// reason CA1724 is suppressed across ktsu.Sdk, and not a reason to spend the name here.
/// </para>
/// </remarks>
public sealed class TrayAppBuilder
{
	private const int ExitSuccess = 0;
	private const int ExitUsage = 2;

	/// <summary>Set to <c>1</c> to print the full exception behind a tray failure.</summary>
	private const string DebugEnvironmentVariable = "TRAYAPP_DEBUG";

	private static readonly TimeSpan DefaultPreferenceDebounce = TimeSpan.FromMilliseconds(500);

	private readonly string name;
	private readonly List<TrayMenuItem> items = [];
	private readonly List<TrayToggleItem> toggles = [];
	private readonly List<AppOption> appOptions = [];
	private readonly List<StatusDetail> statusDetails = [];

	private string? displayName;
	private string? summary;
	private Func<string>? status;
	private TrayIconSet? icons;
	private Func<bool>? isActive;
	private Action? onStart;
	private Action? onStop;
	private Action<Action>? refreshSubscription;
	private IPersistenceProvider<string>? persistence;
	private string preferenceKey = "tray";
	private TimeSpan preferenceDebounce = DefaultPreferenceDebounce;

	private TrayAppBuilder(string name) => this.name = name;

	/// <summary>
	/// Starts describing a tray tool.
	/// </summary>
	/// <param name="name">The command name, as it is typed. It is used in messages and in <c>--help</c>.</param>
	/// <returns>The builder, for chaining.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
	public static TrayAppBuilder Create(string name)
	{
		Ensure.NotNull(name);
		return new TrayAppBuilder(name);
	}

	/// <summary>
	/// Sets the name shown in the menu and in messages, when it differs from the command name.
	/// </summary>
	/// <param name="value">The display name, such as <c>NoSleep</c> for the <c>nosleep</c> command.</param>
	/// <returns>The builder, for chaining.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
	public TrayAppBuilder DisplayName(string value)
	{
		Ensure.NotNull(value);
		displayName = value;
		return this;
	}

	/// <summary>
	/// Sets the sentence printed under the usage line in <c>--help</c>.
	/// </summary>
	/// <param name="value">What the tool does, in one line.</param>
	/// <returns>The builder, for chaining.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
	public TrayAppBuilder Summary(string value)
	{
		Ensure.NotNull(value);
		summary = value;
		return this;
	}

	/// <summary>
	/// Sets what the tool says it is doing.
	/// </summary>
	/// <param name="describe">Produces the sentence, read afresh on every menu refresh.</param>
	/// <returns>The builder, for chaining.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="describe"/> is <see langword="null"/>.</exception>
	/// <remarks>
	/// The text is the disabled line at the top of the menu, the tray icon's tooltip, and the <c>State</c> row
	/// of <c>--status</c>, so it is worth writing as a sentence someone would want to read in all three.
	/// </remarks>
	public TrayAppBuilder Status(Func<string> describe)
	{
		Ensure.NotNull(describe);
		status = describe;
		return this;
	}

	/// <summary>
	/// Adds a label/value row to the <c>--status</c> report.
	/// </summary>
	/// <param name="label">The row's label, without a trailing colon.</param>
	/// <param name="value">Produces the row's value when the report is built.</param>
	/// <returns>The builder, for chaining.</returns>
	/// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
	/// <remarks>
	/// This is where a tool puts the things only it can answer - which mechanism it found, which helper is
	/// missing - next to the platform and tray rows the library already prints.
	/// </remarks>
	public TrayAppBuilder Status(string label, Func<string> value)
	{
		Ensure.NotNull(label);
		Ensure.NotNull(value);
		statusDetails.Add(new StatusDetail(label, value));
		return this;
	}

	/// <summary>
	/// Sets the two tray images.
	/// </summary>
	/// <param name="assembly">The assembly they are embedded in, usually <c>typeof(Program).Assembly</c>.</param>
	/// <param name="activeResourceName">The active image's resource name, in full or relative to the assembly name.</param>
	/// <param name="idleResourceName">The idle image's resource name, resolved the same way.</param>
	/// <returns>The builder, for chaining.</returns>
	/// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
	public TrayAppBuilder Icons(Assembly assembly, string activeResourceName, string idleResourceName)
	{
		icons = TrayIconSet.FromResources(assembly, activeResourceName, idleResourceName);
		return this;
	}

	/// <summary>
	/// Sets one tray image, used whatever the tool's state.
	/// </summary>
	/// <param name="assembly">The assembly it is embedded in.</param>
	/// <param name="resourceName">The image's resource name, in full or relative to the assembly name.</param>
	/// <returns>The builder, for chaining.</returns>
	/// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
	public TrayAppBuilder Icon(Assembly assembly, string resourceName)
	{
		icons = TrayIconSet.FromResource(assembly, resourceName);
		return this;
	}

	/// <summary>
	/// Sets which of the two images the tray shows.
	/// </summary>
	/// <param name="predicate">Reports whether the tool is currently doing its job.</param>
	/// <returns>The builder, for chaining.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="predicate"/> is <see langword="null"/>.</exception>
	/// <remarks>
	/// Without this the first registered toggle decides, which is what a tool with one switch wants and what
	/// the example in <see cref="TrayAppBuilder"/> relies on.
	/// </remarks>
	public TrayAppBuilder ActiveWhen(Func<bool> predicate)
	{
		Ensure.NotNull(predicate);
		isActive = predicate;
		return this;
	}

	/// <summary>
	/// Adds a checkable menu entry.
	/// </summary>
	/// <param name="label">The entry's text.</param>
	/// <param name="getState">Reports whether the toggle is on.</param>
	/// <param name="setState">Sets the toggle. It is given the new value.</param>
	/// <param name="isEnabled">Reports whether the entry can be clicked, or <see langword="null"/> for always.</param>
	/// <param name="persistAs">
	/// The name to remember this toggle under between runs, or <see langword="null"/> not to remember it.
	/// Only meaningful alongside <see cref="Preferences(IPersistenceProvider{string}, string, TimeSpan?)"/>.
	/// </param>
	/// <returns>The builder, for chaining.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="label"/>, <paramref name="getState"/>, or <paramref name="setState"/> is <see langword="null"/>.</exception>
	public TrayAppBuilder Toggle(
		string label,
		Func<bool> getState,
		Action<bool> setState,
		Func<bool>? isEnabled = null,
		string? persistAs = null)
	{
		TrayToggleItem toggle = new(label, getState, setState, isEnabled, persistAs);
		items.Add(toggle);
		toggles.Add(toggle);
		return this;
	}

	/// <summary>
	/// Adds a menu entry that runs an action.
	/// </summary>
	/// <param name="label">The entry's text.</param>
	/// <param name="action">What to run when it is clicked.</param>
	/// <param name="isEnabled">Reports whether the entry can be clicked, or <see langword="null"/> for always.</param>
	/// <returns>The builder, for chaining.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="label"/> or <paramref name="action"/> is <see langword="null"/>.</exception>
	public TrayAppBuilder Command(string label, Action action, Func<bool>? isEnabled = null)
	{
		items.Add(new TrayCommandItem(label, action, isEnabled));
		return this;
	}

	/// <summary>
	/// Adds a horizontal rule between groups of entries.
	/// </summary>
	/// <returns>The builder, for chaining.</returns>
	public TrayAppBuilder Separator()
	{
		items.Add(new TraySeparatorItem());
		return this;
	}

	/// <summary>
	/// Adds a command line switch that takes no value.
	/// </summary>
	/// <param name="aliases">The spellings that select it, such as <c>["-d", "--display"]</c>.</param>
	/// <param name="description">The one-line description shown in <c>--help</c>.</param>
	/// <param name="onSet">Called once when the switch is present.</param>
	/// <returns>The builder, for chaining.</returns>
	/// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
	public TrayAppBuilder Flag(IReadOnlyList<string> aliases, string description, Action onSet)
	{
		appOptions.Add(AppOption.Flag(aliases, description, onSet));
		return this;
	}

	/// <summary>
	/// Adds a command line option that consumes the argument after it.
	/// </summary>
	/// <param name="aliases">The spellings that select it, such as <c>["-r", "--reason"]</c>.</param>
	/// <param name="valueName">The placeholder shown in <c>--help</c>, such as <c>text</c>.</param>
	/// <param name="description">The one-line description shown in <c>--help</c>.</param>
	/// <param name="onValue">
	/// Called with the value. Throw <see cref="FormatException"/> or <see cref="ArgumentException"/> to
	/// reject it: the message becomes the usage error and the tool exits 2.
	/// </param>
	/// <returns>The builder, for chaining.</returns>
	/// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
	public TrayAppBuilder Option(IReadOnlyList<string> aliases, string valueName, string description, Action<string> onValue)
	{
		appOptions.Add(AppOption.Value(aliases, valueName, description, onValue));
		return this;
	}

	/// <summary>
	/// Sets what to run when the tool starts, in both the tray and the console mode.
	/// </summary>
	/// <param name="action">
	/// Takes whatever the tool holds. Throwing reports the refusal: in the console that is a message and exit
	/// code 1, and in the tray it is the status line, with the icon still shown so the user can retry once
	/// whatever was missing is installed.
	/// </param>
	/// <returns>The builder, for chaining.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="action"/> is <see langword="null"/>.</exception>
	public TrayAppBuilder OnStart(Action action)
	{
		Ensure.NotNull(action);
		onStart = action;
		return this;
	}

	/// <summary>
	/// Sets what to run on the way out, in both the tray and the console mode.
	/// </summary>
	/// <param name="action">Releases whatever the tool holds. It runs on Ctrl+C, on <c>SIGTERM</c>, on quit, and when <c>--for</c> expires.</param>
	/// <returns>The builder, for chaining.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="action"/> is <see langword="null"/>.</exception>
	public TrayAppBuilder OnStop(Action action)
	{
		Ensure.NotNull(action);
		onStop = action;
		return this;
	}

	/// <summary>
	/// Gives the tool a way to tell the tray that its state changed without a click.
	/// </summary>
	/// <param name="subscribe">
	/// Called once at startup with a delegate that refreshes the menu; wire it to whatever event the tool
	/// raises, as in <c>refresh =&gt; controller.StateChanged += (_, _) =&gt; refresh()</c>.
	/// </param>
	/// <returns>The builder, for chaining.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="subscribe"/> is <see langword="null"/>.</exception>
	/// <remarks>
	/// The refresh delegate is safe to call from any thread; it posts to the UI thread itself.
	/// </remarks>
	public TrayAppBuilder RefreshOn(Action<Action> subscribe)
	{
		Ensure.NotNull(subscribe);
		refreshSubscription = subscribe;
		return this;
	}

	/// <summary>
	/// Remembers the state of persisted toggles between runs.
	/// </summary>
	/// <param name="provider">
	/// Where the state goes. The library depends on the abstraction only: wire up
	/// <c>ktsu.Essentials.PersistenceProviders.ConfigHome</c> over
	/// <c>ktsu.Essentials.SerializationProviders.Json</c> and
	/// <c>ktsu.Essentials.FileSystemProviders.Native</c> in the tool, and hand the result in here.
	/// </param>
	/// <param name="key">The key the state is stored under. The default suits a provider already namespaced by application.</param>
	/// <param name="debounce">How long a burst of changes is coalesced for, or <see langword="null"/> for half a second.</param>
	/// <returns>The builder, for chaining.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="provider"/> or <paramref name="key"/> is <see langword="null"/>.</exception>
	/// <remarks>
	/// A tool that never calls this does no I/O at all and needs no provider package: persistence is a
	/// dependency of the tool head, never of this library.
	/// </remarks>
	public TrayAppBuilder Preferences(IPersistenceProvider<string> provider, string key = "tray", TimeSpan? debounce = null)
	{
		Ensure.NotNull(provider);
		Ensure.NotNull(key);

		persistence = provider;
		preferenceKey = key;
		preferenceDebounce = debounce ?? DefaultPreferenceDebounce;
		return this;
	}

	/// <summary>
	/// Parses the command line and runs the tool.
	/// </summary>
	/// <param name="args">The arguments, as handed to <c>Main</c>.</param>
	/// <returns>The process exit code: 0 success, 1 the platform refused, 2 the command line was wrong.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="args"/> is <see langword="null"/>.</exception>
	/// <remarks>
	/// Nothing is awaited before the tray starts, on purpose. Avalonia's macOS backend has to run on the
	/// process's main thread, and the continuation after an <c>await</c> in a console application runs on a
	/// thread pool thread instead. The only await is the final preference flush, once the run is over.
	/// </remarks>
	public Task<int> RunAsync(string[] args)
	{
		Ensure.NotNull(args);

		TrayAppDefinition app = Build();

		if (!CommandLineParser.TryParse(args, app.Options, out CommandLineOptions options, out string error))
		{
			return Task.FromResult(ReportUsage(app, error));
		}

		if (options.ShowHelp)
		{
			Console.WriteLine(HelpText.Usage(app.Name, app.Summary, app.Options));
			return Task.FromResult(ExitSuccess);
		}

		if (options.ShowVersion)
		{
			Console.WriteLine(HelpText.Version(app.Name));
			return Task.FromResult(ExitSuccess);
		}

		bool useTray = UseTray(options);
		DebouncedPreferenceStore? store = null;

		try
		{
			// Preferences are the tray's: a command line run says what it wants, so letting a remembered
			// setting override an argument would be surprising, and the console path never reads them.
			if (useTray && app.Persistence is not null)
			{
				store = new DebouncedPreferenceStore(app.Persistence, app.PreferenceKey, app.PreferenceDebounce);
				LoadPreferences(app, store);
			}

			if (!TryApplyOptions(options, out string optionError))
			{
				return Task.FromResult(ReportUsage(app, optionError));
			}

			if (options.ShowStatus)
			{
				Console.WriteLine(StatusReport.Build(app.Name, app.Status?.Invoke(), app.StatusDetails));
				return Task.FromResult(ExitSuccess);
			}

			int exitCode = useTray ? RunTray(app, options, store) : ConsoleRunner.Run(app, options);

			// FinishAsync owns the store from here: it flushes whatever the run queued and disposes it.
			Task<int> finishing = FinishAsync(exitCode, store);
			store = null;
			return finishing;
		}
		finally
		{
			store?.Dispose();
		}
	}

	/// <summary>
	/// Decides between the tray and the console.
	/// </summary>
	/// <remarks>
	/// <c>--no-tray</c> always wins, and <c>--tray</c> overrides the detection for the machines where it
	/// guesses wrong - a working D-Bus status-notifier host with no <c>DISPLAY</c> set, say. Otherwise the
	/// tray is the default wherever there is a session to put it in.
	/// </remarks>
	private static bool UseTray(CommandLineOptions options) =>
		!options.TraySuppressed && (options.TrayRequested || DesktopSession.IsAvailable);

	private static int ReportUsage(TrayAppDefinition app, string error)
	{
		Console.Error.WriteLine($"{app.Name}: {error}");
		Console.Error.WriteLine();
		Console.Error.WriteLine(HelpText.Usage(app.Name, app.Summary, app.Options));
		return ExitUsage;
	}

	private static async Task<int> FinishAsync(int exitCode, DebouncedPreferenceStore? store)
	{
		if (store is null)
		{
			return exitCode;
		}

		try
		{
			await store.FlushAsync().ConfigureAwait(false);
		}
		finally
		{
			store.Dispose();
		}

		return exitCode;
	}

	/// <remarks>
	/// Blocking here is deliberate. The load has to finish before the menu is built, and it has to finish on
	/// the thread that goes on to start Avalonia - see the remarks on <see cref="RunAsync"/>. There is no
	/// synchronization context in a console application, so there is nothing for this to deadlock against.
	/// </remarks>
	[SuppressMessage(
		"Reliability",
		"CA2007:Consider calling ConfigureAwait on the awaited task",
		Justification = "Not an await; the result is taken synchronously so that the tray still starts on the process's main thread.")]
	internal static void LoadPreferences(TrayAppDefinition app, DebouncedPreferenceStore store)
	{
		store.LoadAsync().GetAwaiter().GetResult();

		foreach (TrayToggleItem toggle in app.Toggles)
		{
			if (toggle.PreferenceKey is null || store.Get(toggle.PreferenceKey) is not bool remembered)
			{
				continue;
			}

			toggle.Set(remembered);
		}
	}

	/// <remarks>
	/// Run after the remembered preferences and before anything else, so that what someone typed this time
	/// beats what the tray was left set to last time.
	/// </remarks>
	internal static bool TryApplyOptions(CommandLineOptions options, out string error)
	{
		foreach (AppOptionMatch match in options.Matches)
		{
			try
			{
				match.Option.Apply(match.Value);
			}
			catch (Exception ex) when (ex is FormatException or ArgumentException)
			{
				error = ex.Message;
				return false;
			}
		}

		error = string.Empty;
		return true;
	}

	[SuppressMessage(
		"Design",
		"CA1031:Do not catch general exception types",
		Justification = "Any failure to bring up a tray icon is recoverable by falling back to the console, and the windowing backends do not report those failures through a common exception type.")]
	private static int RunTray(TrayAppDefinition app, CommandLineOptions options, DebouncedPreferenceStore? store)
	{
		TrayMenu menu = new(app.DisplayName, app.Status, app.Items);
		TrayApplication? host = null;

		void Remember(TrayToggleItem toggle)
		{
			if (toggle.PreferenceKey is not null)
			{
				store?.Set(toggle.PreferenceKey, toggle.IsChecked);
			}
		}

		try
		{
			// StartWithClassicDesktopLifetime blocks until the tray quits.
			return AppBuilder.Configure(() => host = new TrayApplication(app, menu, options.Duration, Remember))
				.UsePlatformDetect()
				.With(new MacOSPlatformOptions { ShowInDock = false })
				.StartWithClassicDesktopLifetime([], Avalonia.Controls.ShutdownMode.OnExplicitShutdown);
		}
		catch (Exception ex)
		{
			if (Environment.GetEnvironmentVariable(DebugEnvironmentVariable) == "1")
			{
				Console.Error.WriteLine(ex.ToString());
			}

			// The tray already ran and quit; this is Avalonia's teardown throwing after the fact (see
			// TrayApplication.HasStarted). Restarting in the terminal here would leave a tool the user just
			// quit still holding whatever it holds.
			if (host?.HasStarted == true)
			{
				host.Dispose();
				return ExitSuccess;
			}

			// Otherwise the windowing stack refused after the session check passed - a DISPLAY pointing at
			// nothing, a missing libX11, no status-notifier host on the bus - and it reports those as plain
			// exceptions of whatever type the backend felt like (X11 throws Exception("XOpenDisplay failed")).
			// There is no useful list to filter on, and every one of them means the same thing here: no tray,
			// so use the terminal. Letting it escape instead would kill a process the user asked to keep
			// running.
			host?.Dispose();
			Console.Error.WriteLine($"{app.Name}: could not show a tray icon ({ex.Message}). Staying in the terminal instead.");
			return ConsoleRunner.Run(app, options);
		}
	}

	/// <summary>
	/// Freezes everything the builder was told.
	/// </summary>
	/// <returns>The finished description.</returns>
	internal TrayAppDefinition Build() => new()
	{
		Name = name,
		DisplayName = displayName ?? name,
		Summary = summary,
		Status = status,
		StatusDetails = [.. statusDetails],
		Items = [.. items],
		Toggles = [.. toggles],
		Options = [.. appOptions],
		Icons = icons,
		IsActive = isActive ?? FirstToggleState(),
		OnStart = onStart,
		OnStop = onStop,
		RefreshSubscription = refreshSubscription,
		Persistence = persistence,
		PreferenceKey = preferenceKey,
		PreferenceDebounce = preferenceDebounce,
	};

	private Func<bool>? FirstToggleState()
	{
		if (toggles.Count == 0)
		{
			return null;
		}

		TrayToggleItem first = toggles[0];
		return () =>
		{
			first.Refresh();
			return first.IsChecked;
		};
	}
}
