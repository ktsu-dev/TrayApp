// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.TrayApp.Test;

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using ktsu.Essentials.PersistenceProviders.InMemory;
using ktsu.TrayApp;
using ktsu.TrayApp.Cli;
using ktsu.TrayApp.Menu;
using ktsu.TrayApp.Preferences;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests for what the builder assembles, and for the order in which a run applies it.
/// </summary>
[TestClass]
public class TrayAppBuilderTests
{
	private static readonly TimeSpan Debounce = TimeSpan.FromMilliseconds(50);

	[TestMethod]
	public void Build_WithNoPreferences_TakesNoProvider()
	{
		// A tool that persists nothing must not acquire a persistence dependency by standing still.
		TrayAppDefinition app = TrayAppBuilder.Create("demo").Build();

		Assert.IsNull(app.Persistence);
		Assert.AreEqual("demo", app.Name);
		Assert.AreEqual("demo", app.DisplayName);
	}

	[TestMethod]
	[DataRow("-s")]
	[DataRow("--status")]
	[DataRow("-f")]
	[DataRow("--for")]
	[DataRow("-t")]
	[DataRow("--no-tray")]
	[DataRow("-h")]
	[DataRow("-?")]
	[DataRow("-v")]
	public void Flag_WithAStandardSwitchAlias_Throws(string alias)
	{
		// The standard switches are read first, so an option spelled the same way could never be reached.
		ArgumentException ex = Assert.ThrowsExactly<ArgumentException>(
			() => TrayAppBuilder.Create("demo").Flag(["--quiet", alias], "Quiet.", () => { }));

		Assert.Contains($"'{alias}'", ex.Message, StringComparison.Ordinal);
		Assert.AreEqual("aliases", ex.ParamName);
	}

	[TestMethod]
	public void Option_WithAStandardSwitchAlias_Throws()
	{
		ArgumentException ex = Assert.ThrowsExactly<ArgumentException>(
			() => TrayAppBuilder.Create("demo").Option(["-f", "--file"], "path", "A file.", _ => { }));

		Assert.Contains("'-f'", ex.Message, StringComparison.Ordinal);
	}

	[TestMethod]
	public void Flag_WithAnAliasAnotherOptionTook_Throws()
	{
		TrayAppBuilder builder = TrayAppBuilder.Create("demo").Option(["-d", "--delay"], "time", "Delay.", _ => { });

		ArgumentException ex = Assert.ThrowsExactly<ArgumentException>(
			() => builder.Flag(["-d", "--display"], "Display.", () => { }));

		Assert.Contains("'-d'", ex.Message, StringComparison.Ordinal);
		Assert.AreEqual(1, builder.Build().Options.Count);
	}

	[TestMethod]
	public void Toggle_WithAPersistAsKeyAnotherToggleTook_Throws()
	{
		// Both toggles would read and write one remembered value, so the next launch restores both to it.
		TrayAppBuilder builder = TrayAppBuilder.Create("demo")
			.Toggle("Keep awake", () => false, _ => { }, persistAs: "awake");

		ArgumentException ex = Assert.ThrowsExactly<ArgumentException>(
			() => builder.Toggle("Keep display on", () => false, _ => { }, persistAs: "awake"));

		Assert.Contains("'awake'", ex.Message, StringComparison.Ordinal);
		Assert.AreEqual("persistAs", ex.ParamName);
		Assert.HasCount(1, builder.Build().Toggles);
	}

	[TestMethod]
	public void Toggle_WithPersistAsKeysThatDifferOnlyInCase_KeepsBoth()
	{
		// The store compares keys ordinally, so these are two remembered values.
		TrayAppDefinition app = TrayAppBuilder.Create("demo")
			.Toggle("Keep awake", () => false, _ => { }, persistAs: "awake")
			.Toggle("Keep display on", () => false, _ => { }, persistAs: "Awake")
			.Toggle("Not remembered", () => false, _ => { })
			.Toggle("Not remembered either", () => false, _ => { })
			.Build();

		Assert.HasCount(4, app.Toggles);
	}

	[TestMethod]
	[DataRow("")]
	[DataRow("   ")]
	public void Toggle_WithABlankPersistAsKey_Throws(string key)
	{
		ArgumentException ex = Assert.ThrowsExactly<ArgumentException>(
			() => TrayAppBuilder.Create("demo").Toggle("Keep awake", () => false, _ => { }, persistAs: key));

		Assert.AreEqual("persistAs", ex.ParamName);
	}

	[TestMethod]
	public void Build_CollectsItemsTogglesAndOptions()
	{
		TrayAppDefinition app = TrayAppBuilder.Create("demo")
			.DisplayName("Demo")
			.Summary("does a thing.")
			.Toggle("One", () => false, _ => { }, persistAs: "one")
			.Separator()
			.Command("Two", () => { })
			.Flag(["--three"], "Three.", () => { })
			.Status("Mechanism", () => "none")
			.Build();

		Assert.AreEqual("Demo", app.DisplayName);
		Assert.AreEqual("does a thing.", app.Summary);
		Assert.AreEqual(3, app.Items.Count);
		Assert.AreEqual(1, app.Toggles.Count);
		Assert.AreEqual(1, app.Options.Count);
		Assert.AreEqual(1, app.StatusDetails.Count);
		Assert.AreEqual("Mechanism", app.StatusDetails[0].Label);
	}

	[TestMethod]
	public void Build_WithoutActiveWhen_FollowsTheFirstToggle()
	{
		bool first = false;
		TrayAppDefinition app = TrayAppBuilder.Create("demo")
			.Toggle("First", () => first, value => first = value)
			.Toggle("Second", () => true, _ => { })
			.Build();

		Assert.IsNotNull(app.IsActive);
		Assert.IsFalse(app.IsActive());

		first = true;
		Assert.IsTrue(app.IsActive());
	}

	[TestMethod]
	public void Build_WithActiveWhen_UsesThePredicateInstead()
	{
		TrayAppDefinition app = TrayAppBuilder.Create("demo")
			.Toggle("First", () => false, _ => { })
			.ActiveWhen(() => true)
			.Build();

		Assert.IsTrue(app.IsActive!());
	}

	[TestMethod]
	public void Build_WithNoToggles_HasNoActiveState()
	{
		TrayAppDefinition app = TrayAppBuilder.Create("demo").Build();
		Assert.IsNull(app.IsActive);
	}

	[TestMethod]
	[DataRow(-1000.0)]
	[DataRow(-1.0)]
	[DataRow(60.0 * 24 * 60 * 60 * 1000)]
	public void Preferences_WithADebounceATimerCannotWaitFor_Throws(double milliseconds)
	{
		InMemoryPersistenceProvider<string> provider = new();
		TrayAppBuilder builder = TrayAppBuilder.Create("demo");

		ArgumentOutOfRangeException exception = Assert.ThrowsExactly<ArgumentOutOfRangeException>(
			() => builder.Preferences(provider, debounce: TimeSpan.FromMilliseconds(milliseconds)));
		Assert.AreEqual("debounce", exception.ParamName);
	}

	[TestMethod]
	public void Preferences_WithAnInfiniteDebounce_Throws()
	{
		InMemoryPersistenceProvider<string> provider = new();

		_ = Assert.ThrowsExactly<ArgumentOutOfRangeException>(
			() => TrayAppBuilder.Create("demo").Preferences(provider, debounce: System.Threading.Timeout.InfiniteTimeSpan));
	}

	[TestMethod]
	public void Preferences_WithZeroOrNoDebounce_IsAccepted()
	{
		InMemoryPersistenceProvider<string> provider = new();

		Assert.AreEqual(TimeSpan.Zero, TrayAppBuilder.Create("demo").Preferences(provider, debounce: TimeSpan.Zero).Build().PreferenceDebounce);
		Assert.AreEqual(TimeSpan.FromMilliseconds(500), TrayAppBuilder.Create("demo").Preferences(provider).Build().PreferenceDebounce);
	}

	[TestMethod]
	public void Preferences_WithTheLongestDebounce_IsAcceptedAndATimerTakesIt()
	{
		InMemoryPersistenceProvider<string> provider = new();
		TrayAppDefinition app = TrayAppBuilder.Create("demo")
			.Preferences(provider, debounce: TrayAppBuilder.MaxPreferenceDebounce)
			.Build();

		using DebouncedPreferenceStore store = new(provider, "tray", app.PreferenceDebounce);
		store.Set("a", true);
	}

	[TestMethod]
	public async Task LoadPreferences_RestoresOnlyThePersistedToggles()
	{
		InMemoryPersistenceProvider<string> provider = new();

		using (DebouncedPreferenceStore seed = new(provider, "tray", Debounce))
		{
			await seed.LoadAsync().ConfigureAwait(false);
			seed.Set("remembered", true);
			await seed.FlushAsync().ConfigureAwait(false);
		}

		bool remembered = false;
		bool forgotten = false;

		TrayAppDefinition app = TrayAppBuilder.Create("demo")
			.Toggle("Remembered", () => remembered, value => remembered = value, persistAs: "remembered")
			.Toggle("Forgotten", () => forgotten, value => forgotten = value)
			.Preferences(provider)
			.Build();

		using DebouncedPreferenceStore store = new(provider, "tray", Debounce);
		_ = TrayAppBuilder.LoadPreferences(app, store);

		Assert.IsTrue(remembered);
		Assert.IsFalse(forgotten);
	}

	[TestMethod]
	[DataRow(false)]
	[DataRow(true)]
	public async Task LoadPreferences_DoesNotRestoreAToggleItsParentLeavesDisabled(bool parentRemembered)
	{
		InMemoryPersistenceProvider<string> provider = new();

		using (DebouncedPreferenceStore seed = new(provider, "tray", Debounce))
		{
			await seed.LoadAsync().ConfigureAwait(false);
			seed.Set("awake", parentRemembered);
			seed.Set("display", true);
			await seed.FlushAsync().ConfigureAwait(false);
		}

		// The parent starts in the opposite state, so restoring it is what decides whether the child is enabled.
		bool awake = !parentRemembered;
		bool display = false;
		int displaySetterCalls = 0;

		TrayAppDefinition app = TrayAppBuilder.Create("demo")
			.Toggle("Keep awake", () => awake, value => awake = value, persistAs: "awake")
			.Toggle("Keep display awake too", () => display, value =>
			{
				displaySetterCalls++;
				display = value;
			}, isEnabled: () => awake, persistAs: "display")
			.Preferences(provider)
			.Build();

		using DebouncedPreferenceStore store = new(provider, "tray", Debounce);

		Assert.IsNull(TrayAppBuilder.LoadPreferences(app, store));
		Assert.AreEqual(parentRemembered, awake);
		Assert.AreEqual(parentRemembered, display);
		Assert.AreEqual(parentRemembered ? 1 : 0, displaySetterCalls);
		Assert.IsTrue(store.Get("display") is true, "Expected the skipped toggle's remembered value to stay in the store.");
	}

	[TestMethod]
	[DataRow(false)]
	[DataRow(true)]
	public async Task LoadPreferences_WhenTheToggleIsAlreadyInTheRememberedState_DoesNotCallTheSetter(bool state)
	{
		InMemoryPersistenceProvider<string> provider = new();

		using (DebouncedPreferenceStore seed = new(provider, "tray", Debounce))
		{
			await seed.LoadAsync().ConfigureAwait(false);
			seed.Set("awake", state);
			await seed.FlushAsync().ConfigureAwait(false);
		}

		bool awake = state;
		int setterCalls = 0;

		// The setter ignores its argument and flips, as the class example's does.
		TrayAppDefinition app = TrayAppBuilder.Create("demo")
			.Toggle("Keep awake", () => awake, _ =>
			{
				setterCalls++;
				awake = !awake;
			}, persistAs: "awake")
			.Preferences(provider)
			.Build();

		using DebouncedPreferenceStore store = new(provider, "tray", Debounce);

		Assert.IsNull(TrayAppBuilder.LoadPreferences(app, store));
		Assert.AreEqual(state, awake);
		Assert.AreEqual(0, setterCalls);
	}

	[TestMethod]
	public async Task LoadPreferences_WhenTheGetterThrows_KeepsGoingAndHandsBackTheMessage()
	{
		InMemoryPersistenceProvider<string> provider = new();

		using (DebouncedPreferenceStore seed = new(provider, "tray", Debounce))
		{
			await seed.LoadAsync().ConfigureAwait(false);
			seed.Set("awake", true);
			seed.Set("display", true);
			await seed.FlushAsync().ConfigureAwait(false);
		}

		bool display = false;

		TrayAppDefinition app = TrayAppBuilder.Create("demo")
			.Toggle("Keep awake", () => throw new InvalidOperationException("no inhibitor here"), _ => { }, persistAs: "awake")
			.Toggle("Keep display awake", () => display, value => display = value, persistAs: "display")
			.Preferences(provider)
			.Build();

		using DebouncedPreferenceStore store = new(provider, "tray", Debounce);

		Assert.AreEqual("no inhibitor here", TrayAppBuilder.LoadPreferences(app, store));
		Assert.IsTrue(display);
	}

	[TestMethod]
	public async Task LoadPreferences_ThenOptions_LetsAnExplicitSwitchWin()
	{
		InMemoryPersistenceProvider<string> provider = new();

		using (DebouncedPreferenceStore seed = new(provider, "tray", Debounce))
		{
			await seed.LoadAsync().ConfigureAwait(false);
			seed.Set("running", true);
			await seed.FlushAsync().ConfigureAwait(false);
		}

		bool running = false;

		TrayAppBuilder builder = TrayAppBuilder.Create("demo")
			.Toggle("Running", () => running, value => running = value, persistAs: "running")
			.Flag(["--off"], "Start switched off.", () => running = false)
			.Preferences(provider);

		TrayAppDefinition app = builder.Build();

		using DebouncedPreferenceStore store = new(provider, "tray", Debounce);
		_ = TrayAppBuilder.LoadPreferences(app, store);
		Assert.IsTrue(running, "the remembered value should be restored first");

		Assert.IsTrue(CommandLineParser.TryParse(["--off"], app.Options, out CommandLineOptions options, out _));
		Assert.IsTrue(TrayAppBuilder.TryApplyOptions(options, out _));

		// What was typed this time beats what the tray was left set to last time.
		Assert.IsFalse(running);
	}

	[TestMethod]
	public async Task RecoverFromTrayFailure_FallingBackToTheConsole_UndoesTheRestoredPreferences()
	{
		InMemoryPersistenceProvider<string> provider = await SeedAsync("running", false).ConfigureAwait(false);

		bool running = true;
		bool? runningAtStart = null;

		TrayAppDefinition app = TrayAppBuilder.Create("demo")
			.Toggle("Running", () => running, value => running = value, persistAs: "running")
			.OnStart(() => runningAtStart = running)
			.Preferences(provider)
			.Build();

		using DebouncedPreferenceStore store = new(provider, "tray", Debounce);
		List<RestoredToggle> restored = [];
		_ = TrayAppBuilder.LoadPreferences(app, store, restored);
		Assert.IsFalse(running, "the tray's remembered value should be restored first");

		int exitCode = TrayAppBuilder.RecoverFromTrayFailure(
			app,
			new CommandLineOptions { Duration = TimeSpan.Zero },
			host: null,
			new InvalidOperationException("XOpenDisplay failed"),
			restored);

		// The console run has no menu to turn the toggle back on from, so it must not inherit the tray's state.
		Assert.AreEqual(0, exitCode);
		Assert.IsTrue(runningAtStart, "the console run should start with the default, not the tray's remembered value");
	}

	[TestMethod]
	public async Task RecoverFromTrayFailure_FallingBackToTheConsole_KeepsWhatTheCommandLineAskedFor()
	{
		InMemoryPersistenceProvider<string> provider = await SeedAsync("running", true).ConfigureAwait(false);

		bool running = false;
		bool? runningAtStart = null;

		TrayAppDefinition app = TrayAppBuilder.Create("demo")
			.Toggle("Running", () => running, value => running = value, persistAs: "running")
			.Flag(["--on"], "Start switched on.", () => running = true)
			.OnStart(() => runningAtStart = running)
			.Preferences(provider)
			.Build();

		using DebouncedPreferenceStore store = new(provider, "tray", Debounce);
		List<RestoredToggle> restored = [];
		_ = TrayAppBuilder.LoadPreferences(app, store, restored);

		Assert.IsTrue(CommandLineParser.TryParse(["--on"], app.Options, out CommandLineOptions parsed, out _));
		CommandLineOptions options = new() { Duration = TimeSpan.Zero, Matches = parsed.Matches };
		Assert.IsTrue(TrayAppBuilder.TryApplyOptions(options, out _));

		_ = TrayAppBuilder.RecoverFromTrayFailure(app, options, host: null, new InvalidOperationException("XOpenDisplay failed"), restored);

		// Undoing the restore puts the toggle back to off; the switch typed this time still has to win.
		Assert.IsTrue(runningAtStart);
	}

	[TestMethod]
	public async Task RecoverFromTrayFailure_WhenTheToolRefusesTheUndo_StillRunsInTheConsole()
	{
		InMemoryPersistenceProvider<string> provider = await SeedAsync("running", true).ConfigureAwait(false);

		bool running = false;
		bool restoring = true;
		int starts = 0;

		TrayAppDefinition app = TrayAppBuilder.Create("demo")
			.Toggle(
				"Running",
				() => running,
				value =>
				{
					// Taking the resource back is refused, as an inhibitor can be on any platform.
					if (!restoring)
					{
						throw new InvalidOperationException("no inhibitor here");
					}

					running = value;
				},
				persistAs: "running")
			.OnStart(() => starts++)
			.Preferences(provider)
			.Build();

		using DebouncedPreferenceStore store = new(provider, "tray", Debounce);
		List<RestoredToggle> restored = [];
		_ = TrayAppBuilder.LoadPreferences(app, store, restored);
		restoring = false;

		int exitCode = TrayAppBuilder.RecoverFromTrayFailure(
			app,
			new CommandLineOptions { Duration = TimeSpan.Zero },
			host: null,
			new InvalidOperationException("XOpenDisplay failed"),
			restored);

		// A refused undo is reported, as a refused restore is, and does not cost the user the console run.
		Assert.AreEqual(0, exitCode);
		Assert.AreEqual(1, starts);
	}

	[TestMethod]
	public async Task RecoverFromTrayFailure_WhenTheToggleIsAlreadyBack_DoesNotCallTheSetterAgain()
	{
		InMemoryPersistenceProvider<string> provider = await SeedAsync("running", false).ConfigureAwait(false);

		bool running = true;
		int sets = 0;

		TrayAppDefinition app = TrayAppBuilder.Create("demo")
			.Toggle(
				"Running",
				() => running,
				value =>
				{
					sets++;
					running = value;
				},
				persistAs: "running")
			.Flag(["--on"], "Start switched on.", () => running = true)
			.Preferences(provider)
			.Build();

		using DebouncedPreferenceStore store = new(provider, "tray", Debounce);
		List<RestoredToggle> restored = [];
		_ = TrayAppBuilder.LoadPreferences(app, store, restored);
		Assert.AreEqual(1, sets);

		Assert.IsTrue(CommandLineParser.TryParse(["--on"], app.Options, out CommandLineOptions parsed, out _));
		CommandLineOptions options = new() { Duration = TimeSpan.Zero, Matches = parsed.Matches };
		Assert.IsTrue(TrayAppBuilder.TryApplyOptions(options, out _));

		_ = TrayAppBuilder.RecoverFromTrayFailure(app, options, host: null, new InvalidOperationException("XOpenDisplay failed"), restored);

		// The command line already put the toggle back where it started, so the undo has nothing to set:
		// a setter that takes a resource must not be asked to take it twice.
		Assert.AreEqual(1, sets);
		Assert.IsTrue(running);
	}

	private static async Task<InMemoryPersistenceProvider<string>> SeedAsync(string key, bool value)
	{
		InMemoryPersistenceProvider<string> provider = new();

		using DebouncedPreferenceStore seed = new(provider, "tray", Debounce);
		await seed.LoadAsync().ConfigureAwait(false);
		seed.Set(key, value);
		await seed.FlushAsync().ConfigureAwait(false);

		return provider;
	}

	[TestMethod]
	public async Task LoadPreferences_WhenTheToolRefuses_KeepsGoingAndHandsBackTheMessage()
	{
		InMemoryPersistenceProvider<string> provider = new();

		using (DebouncedPreferenceStore seed = new(provider, "tray", Debounce))
		{
			await seed.LoadAsync().ConfigureAwait(false);
			seed.Set("awake", true);
			seed.Set("display", true);
			await seed.FlushAsync().ConfigureAwait(false);
		}

		bool display = false;

		TrayAppDefinition app = TrayAppBuilder.Create("demo")
			.Toggle("Keep awake", () => false, _ => throw new InvalidOperationException("no inhibitor here"), persistAs: "awake")
			.Toggle("Keep display awake", () => display, value => display = value, persistAs: "display")
			.Preferences(provider)
			.Build();

		using DebouncedPreferenceStore store = new(provider, "tray", Debounce);

		// Restoring "on" asks the tool to take an inhibitor, and the machine may have none to give. That
		// refusal happens before there is a menu to fail into; letting it escape would end the process
		// before the tray appeared.
		string? error = TrayAppBuilder.LoadPreferences(app, store);

		Assert.AreEqual("no inhibitor here", error);

		// The toggle after the one that refused is still restored.
		Assert.IsTrue(display);
	}

	[TestMethod]
	public void LoadPreferences_WhenTheProviderCannotRead_StartsWithDefaultsAndHandsBackTheMessage()
	{
		// A corrupt or hand-edited tray.json, or no file yet in a directory that cannot be written to: the
		// load throws before there is a tray to report it in, and a plain launch must still start.
		FailingPersistenceProvider provider = new() { FailReads = true };
		bool awake = false;
		bool setterCalled = false;

		TrayAppDefinition app = TrayAppBuilder.Create("demo")
			.Toggle(
				"Keep awake",
				() => awake,
				value =>
				{
					setterCalled = true;
					awake = value;
				},
				persistAs: "awake")
			.Preferences(provider)
			.Build();

		using DebouncedPreferenceStore store = new(provider, "tray", Debounce);

		string? error = TrayAppBuilder.LoadPreferences(app, store);

		Assert.AreEqual("Could not load preferences: tray.json is not valid JSON", error);
		Assert.IsFalse(setterCalled);
		Assert.IsNull(store.Get("awake"));
	}

	[TestMethod]
	public async Task FinishAsync_WhenTheProviderCannotWrite_ReportsItAndKeepsTheExitCode()
	{
		// The debounced write has already failed and left the store dirty; the flush on Quit fails again.
		FailingPersistenceProvider provider = new();
		// FinishAsync disposes the store; disposing it again here is a no-op.
		using DebouncedPreferenceStore store = new(provider, "tray", TimeSpan.FromHours(1));
		await store.LoadAsync().ConfigureAwait(false);
		store.Set("awake", true);
		provider.FailWrites = true;

		TextWriter original = Console.Error;
		using StringWriter error = new();
		Console.SetError(error);

		int exitCode;

		try
		{
			exitCode = await TrayAppBuilder.FinishAsync(3, store).ConfigureAwait(false);
		}
		finally
		{
			Console.SetError(original);
		}

		Assert.AreEqual(3, exitCode);
		StringAssert.Contains(error.ToString(), "Could not save preferences: the disk is full");
	}

	[TestMethod]
	public async Task LoadPreferences_WhenNothingRefuses_HandsBackNoMessage()
	{
		InMemoryPersistenceProvider<string> provider = new();

		using (DebouncedPreferenceStore seed = new(provider, "tray", Debounce))
		{
			await seed.LoadAsync().ConfigureAwait(false);
			seed.Set("awake", true);
			await seed.FlushAsync().ConfigureAwait(false);
		}

		bool awake = false;

		TrayAppDefinition app = TrayAppBuilder.Create("demo")
			.Toggle("Keep awake", () => awake, value => awake = value, persistAs: "awake")
			.Preferences(provider)
			.Build();

		using DebouncedPreferenceStore store = new(provider, "tray", Debounce);

		Assert.IsNull(TrayAppBuilder.LoadPreferences(app, store));
		Assert.IsTrue(awake);
	}

	[TestMethod]
	public void TryApplyOptions_WhenAHandlerRejectsTheValue_ReportsItAsAUsageError()
	{
		TrayAppDefinition app = TrayAppBuilder.Create("demo")
			.Option(["--count"], "n", "How many.", value => throw new FormatException($"'{value}' is not a number."))
			.Build();

		Assert.IsTrue(CommandLineParser.TryParse(["--count", "lots"], app.Options, out CommandLineOptions options, out _));
		Assert.IsFalse(TrayAppBuilder.TryApplyOptions(options, out string error));
		Assert.AreEqual("'lots' is not a number.", error);
	}

	[TestMethod]
	public void TryApplyOptions_WhenAHandlerParsesAnOutOfRangeNumber_ReportsItAsAUsageError()
	{
		// int.Parse throws OverflowException, not FormatException, for a number too big for an int, and
		// that used to escape as an unhandled exception (#69).
		TrayAppDefinition app = TrayAppBuilder.Create("demo")
			.Option(["--count"], "n", "How many.", value => _ = int.Parse(value, System.Globalization.CultureInfo.InvariantCulture))
			.Build();

		Assert.IsTrue(CommandLineParser.TryParse(["--count", "99999999999"], app.Options, out CommandLineOptions options, out _));
		Assert.IsFalse(TrayAppBuilder.TryApplyOptions(options, out string error));
		string expected = Assert.ThrowsExactly<OverflowException>(() => int.Parse("99999999999", System.Globalization.CultureInfo.InvariantCulture)).Message;
		Assert.AreEqual(expected, error);
	}

	[TestMethod]
	public async Task RunAsync_WithHelp_PrintsUsageAndSucceeds()
	{
		int exitCode = await TrayAppBuilder.Create("demo")
			.Summary("does a thing.")
			.RunAsync(["--help"])
			.ConfigureAwait(false);

		Assert.AreEqual(0, exitCode);
	}

	[TestMethod]
	public async Task RunAsync_WithABadCommandLine_ExitsTwo()
	{
		int exitCode = await TrayAppBuilder.Create("demo").RunAsync(["--nonsense"]).ConfigureAwait(false);
		Assert.AreEqual(2, exitCode);
	}

	[TestMethod]
	public async Task RunAsync_WithAValueOptionMissingItsValueBeforeStatus_ExitsTwo()
	{
		bool started = false;

		int exitCode = await TrayAppBuilder.Create("demo")
			.Option(["-r", "--reason"], "text", "Why.", _ => { })
			.OnStart(() => started = true)
			.RunAsync(["--no-tray", "--for", "0.05s", "--reason", "--status"])
			.ConfigureAwait(false);

		Assert.AreEqual(2, exitCode);
		Assert.IsFalse(started);
	}

	[TestMethod]
	public async Task RunAsync_WithStatus_ReportsWithoutStarting()
	{
		bool started = false;

		int exitCode = await TrayAppBuilder.Create("demo")
			.Status(() => "idle")
			.OnStart(() => started = true)
			.RunAsync(["--status"])
			.ConfigureAwait(false);

		Assert.AreEqual(0, exitCode);
		Assert.IsFalse(started);
	}

	[TestMethod]
	[DoNotParallelize]
	public async Task RunAsync_WithStatusAndThrowingGetters_PrintsEveryRowAndExitsZero()
	{
		TextWriter original = Console.Out;
		using StringWriter output = new();
		Console.SetOut(output);
		int exitCode;
		try
		{
			exitCode = await TrayAppBuilder.Create("demo")
				.Status(() => throw new InvalidOperationException("state unknown"))
				.Status("Inhibitor", () => throw new InvalidOperationException("systemd-inhibit was not found on PATH"))
				.Status("Helper", () => "present")
				.RunAsync(["--no-tray", "--status"])
				.ConfigureAwait(false);
		}
		finally
		{
			Console.SetOut(original);
		}

		string report = output.ToString();
		Assert.AreEqual(0, exitCode);
		Assert.Contains("Platform:", report, StringComparison.Ordinal);
		Assert.Contains("State:         error: state unknown", report, StringComparison.Ordinal);
		Assert.Contains("Inhibitor:     error: systemd-inhibit was not found on PATH", report, StringComparison.Ordinal);
		Assert.Contains("Helper:", report, StringComparison.Ordinal);
	}

	[TestMethod]
	public async Task RunAsync_WithStatusAndARememberedToggle_RunsNoSetterAndReportsTheLiveState()
	{
		InMemoryPersistenceProvider<string> provider = new();

		using (DebouncedPreferenceStore seed = new(provider, "tray", Debounce))
		{
			await seed.LoadAsync().ConfigureAwait(false);
			seed.Set("awake", true);
			await seed.FlushAsync().ConfigureAwait(false);
		}

		bool awake = false;
		int setterCalls = 0;
		bool? awakeWhenReported = null;

		int exitCode = await TrayAppBuilder.Create("demo")
			.Toggle("Keep awake", () => awake, value =>
			{
				setterCalls++;
				awake = value;
			}, persistAs: "awake")
			.Status(() =>
			{
				awakeWhenReported = awake;
				return awake ? "Keeping awake" : "Idle";
			})
			.Preferences(provider)
			.RunAsync(["--tray", "--status"])
			.ConfigureAwait(false);

		// A status query must not take what the toggle guards: it exits without OnStop to give it back.
		Assert.AreEqual(0, exitCode);
		Assert.AreEqual(0, setterCalls);
		Assert.IsFalse(awakeWhenReported);
	}

	[TestMethod]
	public void Menu_BuiltFromADefinition_CarriesTheToolsItems()
	{
		TrayAppDefinition app = TrayAppBuilder.Create("demo")
			.DisplayName("Demo")
			.Status(() => "idle")
			.Toggle("One", () => false, _ => { })
			.Build();

		TrayMenu menu = new(app.DisplayName, app.Status, app.Items);
		menu.Refresh();

		IReadOnlyList<TrayMenuItem> items = menu.Items;

		Assert.AreEqual(5, items.Count);
		Assert.AreEqual("idle", items[0].Header);
		Assert.AreEqual("One", items[2].Header);
		Assert.AreEqual("Quit Demo", items[4].Header);
	}
}
