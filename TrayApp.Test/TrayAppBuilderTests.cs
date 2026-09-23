// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.TrayApp.Test;

using System;
using System.Collections.Generic;
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
