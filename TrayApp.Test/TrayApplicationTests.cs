// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.TrayApp.Test;

using System.Linq;
using Avalonia.Controls;
using ktsu.TrayApp;
using ktsu.TrayApp.Cli;
using ktsu.TrayApp.Menu;
using ktsu.TrayApp.Tray;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests for the parts of the Avalonia host that do not need a display.
/// </summary>
/// <remarks>
/// That there are any is the point. The host builds its native menu items in the constructor and leaves the
/// <see cref="TrayIcon"/> until <c>OnFrameworkInitializationCompleted</c>, because a <c>TrayIcon</c>'s
/// constructor reaches for a platform handle that does not exist until Avalonia has started and a
/// <c>NativeMenuItem</c> does not. If that split is ever undone, these tests stop constructing.
/// </remarks>
[TestClass]
public class TrayApplicationTests
{
	private static TrayAppDefinition Definition(bool withStatus = true) =>
		TrayAppBuilder.Create("demo")
			.DisplayName("Demo")
			.Status(withStatus ? () => "idle" : null!)
			.Toggle("One", () => true, _ => { })
			.Separator()
			.Command("Two", () => { })
			.Build();

	private static TrayApplication Host(TrayAppDefinition app, out TrayMenu menu)
	{
		menu = new TrayMenu(app.DisplayName, app.Status, app.Items);
		return new TrayApplication(app, menu, duration: null, onToggled: null);
	}

	[TestMethod]
	public void Constructor_BuildsTheMenuWithoutAPlatform()
	{
		using TrayApplication host = Host(Definition(), out TrayMenu menu);

		Assert.IsFalse(host.HasStarted);
		Assert.AreEqual(menu.Items.Count, host.NativeMenu.Items.Count);
	}

	[TestMethod]
	public void Constructor_ProjectsEachKindOntoTheRightNativeItem()
	{
		using TrayApplication host = Host(Definition(), out TrayMenu menu);

		// status, separator, toggle, separator, command, separator, quit
		string[] shapes = [.. host.NativeMenu.Items.Select(item => item is NativeMenuItemSeparator ? "separator" : "item")];

		CollectionAssert.AreEqual(
			new[] { "item", "separator", "item", "separator", "item", "separator", "item" },
			shapes);

		NativeMenuItem toggle = (NativeMenuItem)host.NativeMenu.Items[2];
		Assert.AreEqual(MenuItemToggleType.CheckBox, toggle.ToggleType);
		Assert.AreEqual("One", toggle.Header);

		NativeMenuItem command = (NativeMenuItem)host.NativeMenu.Items[4];
		Assert.AreEqual(MenuItemToggleType.None, command.ToggleType);

		NativeMenuItem quit = (NativeMenuItem)host.NativeMenu.Items[6];
		Assert.AreEqual("Quit Demo", quit.Header);
	}

	[TestMethod]
	public void Constructor_WithNoStatus_LeavesTheStatusLineOut()
	{
		TrayAppDefinition app = TrayAppBuilder.Create("demo")
			.DisplayName("Demo")
			.Toggle("One", () => false, _ => { })
			.Build();

		using TrayApplication host = Host(app, out TrayMenu menu);

		Assert.IsNull(menu.StatusItem);
		Assert.AreEqual(menu.Items.Count, host.NativeMenu.Items.Count);
	}

	[TestMethod]
	public void Dispose_BeforeTheTrayStarted_DoesNotStopTheTool()
	{
		bool stopped = false;

		TrayAppDefinition app = TrayAppBuilder.Create("demo")
			.DisplayName("Demo")
			.Toggle("One", () => false, _ => { })
			.OnStop(() => stopped = true)
			.Build();

		TrayApplication host = Host(app, out _);
		host.Dispose();

		// The start action runs in OnFrameworkInitializationCompleted, so a tray that never came up never
		// started the tool. Stopping it here would release something the tool does not hold, and the console
		// fallback that follows would then start and stop it a second time.
		Assert.IsFalse(stopped);
	}

	[TestMethod]
	public void Dispose_IsIdempotent()
	{
		int stops = 0;

		TrayAppDefinition app = TrayAppBuilder.Create("demo")
			.DisplayName("Demo")
			.Toggle("One", () => false, _ => { })
			.OnStop(() => stops++)
			.Build();

		TrayApplication host = Host(app, out _);

		host.Dispose();
		host.Dispose();

		// Disposing twice must not release twice. The fallback path disposes the host itself after the run
		// call throws, and the lifetime's Exit handler has usually disposed it already by then.
		Assert.AreEqual(0, stops);
	}

	[TestMethod]
	public void TrayIconClick_WithTheToggleDisabled_DoesNotRunTheToolsSetter()
	{
		int setterCalls = 0;
		bool available = false;

		TrayAppDefinition app = TrayAppBuilder.Create("demo")
			.DisplayName("Demo")
			.Toggle("Keep awake", () => false, _ => setterCalls++, isEnabled: () => available)
			.Build();

		using TrayApplication host = Host(app, out TrayMenu menu);

		// What painting the menu would have read. IsEnabled is a cache of the last refresh, so without this
		// the item still carries its default of enabled and the assertion would pass for the wrong reason.
		menu.Refresh();

		host.OnTrayIconClicked(this, EventArgs.Empty);

		// The menu shows this greyed out, so a click on the icon has to agree with it. The native menu entry
		// is refused by the toolkit; the icon is the path with nothing in the way.
		Assert.AreEqual(0, setterCalls);
	}

	[TestMethod]
	public void TrayIconClick_WithTheToggleEnabled_RunsTheToolsSetter()
	{
		int setterCalls = 0;

		TrayAppDefinition app = TrayAppBuilder.Create("demo")
			.DisplayName("Demo")
			.Toggle("Keep awake", () => false, _ => setterCalls++, isEnabled: () => true)
			.Build();

		using TrayApplication host = Host(app, out TrayMenu menu);
		menu.Refresh();

		host.OnTrayIconClicked(this, EventArgs.Empty);

		// The fast path still has to work. Guarding it is worth nothing if it guards everything.
		Assert.AreEqual(1, setterCalls);
	}

	[TestMethod]
	public void TrayIconClick_BecomingEnabledAgain_RunsTheToolsSetter()
	{
		int setterCalls = 0;
		bool available = false;

		TrayAppDefinition app = TrayAppBuilder.Create("demo")
			.DisplayName("Demo")
			.Toggle("Keep awake", () => false, _ => setterCalls++, isEnabled: () => available)
			.Build();

		using TrayApplication host = Host(app, out TrayMenu menu);

		menu.Refresh();
		host.OnTrayIconClicked(this, EventArgs.Empty);

		available = true;
		menu.Refresh();
		host.OnTrayIconClicked(this, EventArgs.Empty);

		// Refused while the tool said it was unavailable, served once it said otherwise. The guard reads the
		// refreshed state rather than latching on the first answer.
		Assert.AreEqual(1, setterCalls);
	}

	[TestMethod]
	public void Activate_WithTheItemDisabled_DoesNotRunTheToolsSetter()
	{
		int setterCalls = 0;
		bool available = true;

		TrayAppDefinition app = TrayAppBuilder.Create("demo")
			.DisplayName("Demo")
			.Toggle("Keep awake", () => false, _ => setterCalls++, isEnabled: () => available)
			.Build();

		using TrayApplication host = Host(app, out TrayMenu menu);

		menu.Refresh();
		available = false;
		menu.Refresh();

		TrayMenuItem toggle = menu.Items.First(item => item is TrayToggleItem);
		host.Activate(toggle);

		// The backstop behind the native menu. The toolkit will not raise Click for a greyed-out item, but
		// it greys it out as of the last refresh, and a click already on its way when the tool became
		// unavailable still arrives here. NativeMenuItem exposes Click with no way to raise it, so this
		// calls the handler's target rather than the event.
		Assert.AreEqual(0, setterCalls);
	}

	[TestMethod]
	public void TrayIconClick_WithNoToggleAtAll_DoesNothing()
	{
		int commandCalls = 0;

		TrayAppDefinition app = TrayAppBuilder.Create("demo")
			.DisplayName("Demo")
			.Command("Two", () => commandCalls++)
			.Build();

		using TrayApplication host = Host(app, out TrayMenu menu);
		menu.Refresh();

		host.OnTrayIconClicked(this, EventArgs.Empty);

		// The fast path is for toggles. A menu of commands must not have one picked for it, and quit least
		// of all.
		Assert.AreEqual(0, commandCalls);
	}

	[TestMethod]
	public void StartTool_WhenTheStartActionSucceeds_KeepsTheSeededRestoreError()
	{
		TrayAppDefinition app = TrayAppBuilder.Create("demo")
			.DisplayName("Demo")
			.Status(() => "idle")
			.Toggle("One", () => false, _ => { })
			.OnStart(() => { })
			.Build();

		using TrayApplication host = Host(app, out TrayMenu menu);
		menu.SeedError("no inhibitor");

		host.StartTool();

		// The remembered setting was not applied, and a successful start does not change that. Clearing the
		// message here would put a healthy status line in front of the user before the menu is ever painted.
		Assert.AreEqual("no inhibitor", menu.LastError);
		Assert.AreEqual("Demo failed: no inhibitor", menu.StatusItem!.Header);
	}

	[TestMethod]
	public void StartTool_WhenTheStartActionFails_ReportsItsOwnError()
	{
		TrayAppDefinition app = TrayAppBuilder.Create("demo")
			.DisplayName("Demo")
			.Status(() => "idle")
			.Toggle("One", () => false, _ => { })
			.OnStart(() => throw new InvalidOperationException("device busy"))
			.Build();

		using TrayApplication host = Host(app, out TrayMenu menu);
		menu.SeedError("no inhibitor");

		host.StartTool();

		// The start refusal is the newer and more pressing failure, so it wins the one status line.
		Assert.AreEqual("device busy", menu.LastError);
	}

	[TestMethod]
	public void Dispose_AfterTheToolStartedButBeforeTheTrayFinishedStarting_StopsTheTool()
	{
		int stops = 0;

		TrayAppDefinition app = TrayAppBuilder.Create("demo")
			.DisplayName("Demo")
			.Toggle("One", () => false, _ => { })
			.OnStart(() => { })
			.OnStop(() => stops++)
			.Build();

		TrayApplication host = Host(app, out _);
		host.StartTool();
		host.Dispose();

		// Whatever threw between the start action and the end of start-up, the tool is holding what it took,
		// and nothing else will release it.
		Assert.IsFalse(host.HasStarted);
		Assert.AreEqual(1, stops);
	}

	[TestMethod]
	public void RecoverFromTrayFailure_AfterTheToolStarted_DoesNotStartItAgain()
	{
		int starts = 0;
		int stops = 0;

		TrayAppDefinition app = TrayAppBuilder.Create("demo")
			.DisplayName("Demo")
			.Toggle("One", () => false, _ => { })
			.OnStart(() => starts++)
			.OnStop(() => stops++)
			.Build();

		using TrayApplication host = Host(app, out _);
		host.StartTool();

		// What a missing icon resource or a throwing status getter looks like from RunTray: start-up threw
		// after the start action had run, so HasStarted is still false.
		int exitCode = TrayAppBuilder.RecoverFromTrayFailure(
			app,
			new CommandLineOptions { Duration = TimeSpan.Zero },
			host,
			new InvalidOperationException("The tray icon 'nope-idle.png' is missing"));

		Assert.AreEqual(1, starts);
		Assert.AreEqual(1, stops);
		Assert.AreEqual(1, exitCode);
	}

	[TestMethod]
	public void RecoverFromTrayFailure_BeforeTheToolStarted_FallsBackToTheConsole()
	{
		int starts = 0;
		int stops = 0;

		TrayAppDefinition app = TrayAppBuilder.Create("demo")
			.DisplayName("Demo")
			.Toggle("One", () => false, _ => { })
			.OnStart(() => starts++)
			.OnStop(() => stops++)
			.Build();

		using TrayApplication host = Host(app, out _);

		int exitCode = TrayAppBuilder.RecoverFromTrayFailure(
			app,
			new CommandLineOptions { Duration = TimeSpan.Zero },
			host,
			new InvalidOperationException("XOpenDisplay failed"));

		// The windowing stack refused before anything was started, so the terminal run is the only start.
		Assert.AreEqual(1, starts);
		Assert.AreEqual(1, stops);
		Assert.AreEqual(0, exitCode);
	}

	[TestMethod]
	public void StartTool_WithNoStartAction_KeepsTheSeededErrorAndRecordsTheStart()
	{
		using TrayApplication host = Host(Definition(), out TrayMenu menu);
		menu.SeedError("no inhibitor");

		host.StartTool();

		// There is no start action to succeed, so nothing clears the restore error; the tool still counts as
		// started, so a later failure stops it rather than starting it again in the terminal.
		Assert.IsTrue(host.ToolStarted);
		Assert.AreEqual("no inhibitor", menu.LastError);
	}

	[TestMethod]
	public void RecoverFromTrayFailure_WithNoHost_FallsBackToTheConsole()
	{
		int starts = 0;

		TrayAppDefinition app = TrayAppBuilder.Create("demo")
			.DisplayName("Demo")
			.Toggle("One", () => false, _ => { })
			.OnStart(() => starts++)
			.Build();

		// Avalonia can refuse before it ever asks for the application, leaving no host to dispose.
		int exitCode = TrayAppBuilder.RecoverFromTrayFailure(
			app,
			new CommandLineOptions { Duration = TimeSpan.Zero },
			host: null,
			new InvalidOperationException("XOpenDisplay failed"));

		Assert.AreEqual(1, starts);
		Assert.AreEqual(0, exitCode);
	}

	[TestMethod]
	public void RecoverFromTrayFailure_WithDebugOutputOn_StillStopsTheToolOnce()
	{
		int stops = 0;

		TrayAppDefinition app = TrayAppBuilder.Create("demo")
			.DisplayName("Demo")
			.Toggle("One", () => false, _ => { })
			.OnStop(() => stops++)
			.Build();

		using TrayApplication host = Host(app, out _);
		host.StartTool();

		string? previous = Environment.GetEnvironmentVariable("TRAYAPP_DEBUG");

		try
		{
			Environment.SetEnvironmentVariable("TRAYAPP_DEBUG", "1");

			int exitCode = TrayAppBuilder.RecoverFromTrayFailure(
				app,
				new CommandLineOptions { Duration = TimeSpan.Zero },
				host,
				new InvalidOperationException("status getter threw"));

			// Printing the full exception is diagnostic only; it must not change what the run ends as.
			Assert.AreEqual(1, exitCode);
			Assert.AreEqual(1, stops);
		}
		finally
		{
			Environment.SetEnvironmentVariable("TRAYAPP_DEBUG", previous);
		}
	}
}
