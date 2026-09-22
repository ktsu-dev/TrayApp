// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.TrayApp.Test;

using System;
using System.Collections.Generic;
using ktsu.TrayApp;
using ktsu.TrayApp.Cli;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests for the headless mode.
/// </summary>
/// <remarks>
/// This is the mode a CI agent, an SSH session or a script gets, and the mode the tray falls back to when
/// the windowing stack refuses, so it is the one path a tool can end up on without anybody choosing it. The
/// runs here are bounded by a short <c>--for</c> rather than by a signal: delivering a real <c>SIGTERM</c>
/// to the test host would race the handler's own registration, and losing that race kills the run.
/// </remarks>
[TestClass]
public class ConsoleRunnerTests
{
	private static readonly IReadOnlyList<AppOption> NoOptions = [];

	private static CommandLineOptions ShortRun() =>
		new() { TraySuppressed = true, Duration = TimeSpan.FromMilliseconds(50) };

	[TestMethod]
	public void Run_WithADuration_StartsStopsAndSucceeds()
	{
		List<string> order = [];

		TrayAppDefinition app = TrayAppBuilder.Create("demo")
			.Status(() => "holding")
			.OnStart(() => order.Add("start"))
			.OnStop(() => order.Add("stop"))
			.Build();

		Assert.AreEqual(0, ConsoleRunner.Run(app, ShortRun()));
		CollectionAssert.AreEqual(new[] { "start", "stop" }, order);
	}

	[TestMethod]
	public void Run_WithNoLifecycleActions_StillSucceeds()
	{
		TrayAppDefinition app = TrayAppBuilder.Create("demo").Build();

		Assert.AreEqual(0, ConsoleRunner.Run(app, ShortRun()));
	}

	[TestMethod]
	public void Run_WhenTheToolRefusesToStart_ExitsOneWithoutStopping()
	{
		bool stopped = false;

		TrayAppDefinition app = TrayAppBuilder.Create("demo")
			.OnStart(() => throw new InvalidOperationException("no device here"))
			.OnStop(() => stopped = true)
			.Build();

		// Exit code 1 is "the platform refused", which is what --help promises. Stopping something that
		// never started would release an inhibitor the tool does not hold.
		Assert.AreEqual(1, ConsoleRunner.Run(app, ShortRun()));
		Assert.IsFalse(stopped);
	}

	[TestMethod]
	public void Run_WhenTheRunItselfThrows_StillStops()
	{
		bool stopped = false;

		TrayAppDefinition app = TrayAppBuilder.Create("demo")
			.Status(() => throw new InvalidOperationException("the status broke"))
			.OnStop(() => stopped = true)
			.Build();

		// Describing the run is the tool's code too. Whatever it does, the release on the way out is the
		// library's promise, so it happens in a finally rather than after the last statement.
		Assert.ThrowsExactly<InvalidOperationException>(() => ConsoleRunner.Run(app, ShortRun()));
		Assert.IsTrue(stopped);
	}

	[TestMethod]
	public void Run_WithoutADuration_IsWhatTheParserProducesForNoForFlag()
	{
		// Guarding the shape rather than the wait: an unbounded Run() blocks until a signal, which a unit
		// test has no safe way to deliver.
		Assert.IsTrue(CommandLineParser.TryParse(["--no-tray"], NoOptions, out CommandLineOptions options, out _));
		Assert.IsNull(options.Duration);
		Assert.IsTrue(options.TraySuppressed);
	}

	[TestMethod]
	public async System.Threading.Tasks.Task RunAsync_WithNoTray_RunsTheConsolePathEndToEnd()
	{
		List<string> order = [];

		int exitCode = await TrayAppBuilder.Create("demo")
			.Status(() => "holding")
			.OnStart(() => order.Add("start"))
			.OnStop(() => order.Add("stop"))
			.RunAsync(["--no-tray", "--for", "0.05s"])
			.ConfigureAwait(false);

		Assert.AreEqual(0, exitCode);
		CollectionAssert.AreEqual(new[] { "start", "stop" }, order);
	}

	[TestMethod]
	public async System.Threading.Tasks.Task RunAsync_WithNoTray_IgnoresRememberedPreferences()
	{
		bool running = false;

		ktsu.Essentials.PersistenceProviders.InMemory.InMemoryPersistenceProvider<string> provider = new();

		using (Preferences.DebouncedPreferenceStore seed = new(provider, "tray", TimeSpan.FromMilliseconds(20)))
		{
			await seed.LoadAsync().ConfigureAwait(false);
			seed.Set("running", true);
			await seed.FlushAsync().ConfigureAwait(false);
		}

		int exitCode = await TrayAppBuilder.Create("demo")
			.Toggle("Running", () => running, value => running = value, persistAs: "running")
			.Preferences(provider)
			.RunAsync(["--no-tray", "--for", "0.05s"])
			.ConfigureAwait(false);

		Assert.AreEqual(0, exitCode);

		// A command line run is explicit about what it wants, so the tray's remembered state stays out of it.
		Assert.IsFalse(running);
	}
}
