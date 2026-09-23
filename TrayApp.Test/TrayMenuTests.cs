// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.TrayApp.Test;

using System;
using System.Linq;
using ktsu.TrayApp.Menu;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests for the menu model: what it is made of, and what one refresh brings back in line.
/// </summary>
/// <remarks>
/// The model is free of Avalonia precisely so this can run on a build agent with no display, which is where
/// the state-refresh contract is worth pinning down: every visible property comes from a getter, so nothing
/// the application does behind the menu's back can leave it showing a stale tick.
/// </remarks>
[TestClass]
public class TrayMenuTests
{
	private static TrayMenu BuildMenu(Func<string>? status, params TrayMenuItem[] items) =>
		new("Demo", status, items);

	// The item constructors are internal, which is the whole reason this assembly is named in
	// InternalsVisibleTo: a menu can be assembled here without going near the builder or Avalonia.
	private static TrayToggleItem Toggle(string label, Func<bool> get, Action<bool> set, Func<bool>? enabled = null, string? persistAs = null) =>
		new(label, get, set, enabled, persistAs);

	[TestMethod]
	public void Constructor_AppendsASeparatorAndQuit()
	{
		TrayMenu menu = BuildMenu(status: null);

		Assert.AreEqual(2, menu.Items.Count);
		Assert.IsInstanceOfType<TraySeparatorItem>(menu.Items[0]);
		Assert.AreSame(menu.QuitItem, menu.Items[1]);
		Assert.AreEqual("Quit Demo", menu.QuitItem.Header);
	}

	[TestMethod]
	public void Constructor_WithAStatus_PutsADisabledLineAtTheTop()
	{
		TrayMenu menu = BuildMenu(() => "holding");
		menu.Refresh();

		Assert.IsNotNull(menu.StatusItem);
		Assert.AreSame(menu.StatusItem, menu.Items[0]);
		Assert.AreEqual("holding", menu.StatusItem.Header);
		Assert.IsFalse(menu.StatusItem.IsEnabled);
	}

	[TestMethod]
	public void Refresh_ReadsEveryItemsStateBack()
	{
		bool on = false;
		bool available = true;

		TrayToggleItem toggle = Toggle("Switch", () => on, value => on = value, () => available);
		TrayMenu menu = BuildMenu(() => on ? "on" : "off", toggle);

		menu.Refresh();
		Assert.IsFalse(toggle.IsChecked);
		Assert.IsTrue(toggle.IsEnabled);
		Assert.AreEqual("off", menu.StatusItem!.Header);

		// Changed behind the menu's back, as a timer or a device event would.
		on = true;
		available = false;

		menu.Refresh();
		Assert.IsTrue(toggle.IsChecked);
		Assert.IsFalse(toggle.IsEnabled);
		Assert.AreEqual("on", menu.StatusItem.Header);
	}

	[TestMethod]
	public void Activate_FlipsAToggleAndRefreshes()
	{
		bool on = false;
		TrayToggleItem toggle = Toggle("Switch", () => on, value => on = value);
		TrayMenu menu = BuildMenu(status: null, toggle);

		Assert.IsTrue(menu.Activate(toggle));

		Assert.IsTrue(on);
		Assert.IsTrue(toggle.IsChecked);
		Assert.IsNull(menu.LastError);
	}

	[TestMethod]
	public void Activate_ReadsTheCurrentValueRatherThanTheLastPaintedOne()
	{
		bool on = false;
		TrayToggleItem toggle = Toggle("Switch", () => on, value => on = value);
		TrayMenu menu = BuildMenu(status: null, toggle);

		menu.Refresh();

		// Something else turned it on since the menu was painted; clicking has to turn it off.
		on = true;

		Assert.IsTrue(menu.Activate(toggle));
		Assert.IsFalse(on);
	}

	[TestMethod]
	public void Activate_WhenTheToolRefuses_KeepsTheMessageAndSurvivesTheRefresh()
	{
		bool on = false;
		TrayToggleItem toggle = Toggle("Switch", () => on, _ => throw new InvalidOperationException("no inhibitor here"));
		TrayMenu menu = BuildMenu(() => "idle", toggle);

		Assert.IsFalse(menu.Activate(toggle));

		// The refresh that Activate runs immediately afterwards would overwrite a message written straight
		// into the status item, which is why the menu keeps it in a field and composes the header from it.
		Assert.AreEqual("no inhibitor here", menu.LastError);
		Assert.AreEqual("Demo failed: no inhibitor here", menu.StatusItem!.Header);

		Assert.IsTrue(menu.Activate(menu.QuitItem));
		Assert.IsNull(menu.LastError);
		Assert.AreEqual("idle", menu.StatusItem.Header);
	}

	[TestMethod]
	public void SeedError_ShowsInTheStatusLineAndClearsOnTheNextSuccess()
	{
		TrayMenu menu = BuildMenu(() => "idle");

		// A refusal while restoring a remembered toggle happens before the menu exists, so the host seeds it.
		menu.SeedError("no inhibitor here");
		menu.Refresh();

		Assert.AreEqual("no inhibitor here", menu.LastError);
		Assert.AreEqual("Demo failed: no inhibitor here", menu.StatusItem!.Header);

		Assert.IsTrue(menu.Run(() => { }));
		Assert.IsNull(menu.LastError);
		Assert.AreEqual("idle", menu.StatusItem.Header);
	}

	[TestMethod]
	public void Activate_OnTheQuitItem_RaisesQuitRequested()
	{
		TrayMenu menu = BuildMenu(status: null);
		int quits = 0;
		menu.QuitRequested += (_, _) => quits++;

		Assert.IsTrue(menu.Activate(menu.QuitItem));
		Assert.AreEqual(1, quits);
	}

	[TestMethod]
	public void Activate_OnACommand_RunsIt()
	{
		int runs = 0;
		TrayCommandItem command = new("Do it", () => runs++, getEnabled: null);
		TrayMenu menu = BuildMenu(status: null, command);

		Assert.IsTrue(menu.Activate(command));
		Assert.AreEqual(1, runs);
	}

	[TestMethod]
	public void Run_WhenTheToolRefuses_RecordsItWithoutThrowing()
	{
		TrayMenu menu = BuildMenu(() => "idle");

		Assert.IsFalse(menu.Run(() => throw new InvalidOperationException("nope")));
		Assert.AreEqual("nope", menu.LastError);
	}

	[TestMethod]
	public void Items_KeepTheOrderTheToolRegisteredThemIn()
	{
		TrayMenu menu = BuildMenu(
			() => "idle",
			new TrayToggleItem("First", () => false, _ => { }, getEnabled: null, preferenceKey: null),
			new TraySeparatorItem(),
			new TrayCommandItem("Second", () => { }, getEnabled: null));

		string[] shapes = [.. menu.Items.Select(item => item.GetType().Name)];

		CollectionAssert.AreEqual(
			new[]
			{
				nameof(TrayStatusItem),
				nameof(TraySeparatorItem),
				nameof(TrayToggleItem),
				nameof(TraySeparatorItem),
				nameof(TrayCommandItem),
				nameof(TraySeparatorItem),
				nameof(TrayCommandItem),
			},
			shapes);
	}

	[TestMethod]
	public void Toggle_RemembersThePreferenceKeyItWasGiven()
	{
		TrayToggleItem toggle = Toggle("Switch", () => false, _ => { }, persistAs: "switch");
		Assert.AreEqual("switch", toggle.PreferenceKey);

		TrayToggleItem unsaved = Toggle("Switch", () => false, _ => { });
		Assert.IsNull(unsaved.PreferenceKey);
	}
}
