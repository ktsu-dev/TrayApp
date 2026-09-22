// Copyright (c) 2026 ktsu-dev contributors

namespace ktsu.TrayApp.Test;

using System;
using System.Collections.Generic;
using ktsu.TrayApp.Cli;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests for <see cref="CommandLineParser"/> and the options a tool registers on top of the standard set.
/// </summary>
[TestClass]
public class CommandLineParserTests
{
	private static readonly IReadOnlyList<AppOption> NoOptions = [];

	[TestMethod]
	public void TryParse_WithNoArguments_ReturnsDefaults()
	{
		Assert.IsTrue(CommandLineParser.TryParse([], NoOptions, out CommandLineOptions options, out string error));

		Assert.AreEqual(string.Empty, error);
		Assert.IsFalse(options.TrayRequested);
		Assert.IsFalse(options.TraySuppressed);
		Assert.IsFalse(options.ShowStatus);
		Assert.IsFalse(options.ShowHelp);
		Assert.IsFalse(options.ShowVersion);
		Assert.IsNull(options.Duration);
		Assert.AreEqual(0, options.Matches.Count);
	}

	[TestMethod]
	[DataRow("-t")]
	[DataRow("--tray")]
	public void TryParse_WithTray_SetsTrayRequested(string argument)
	{
		Assert.IsTrue(CommandLineParser.TryParse([argument], NoOptions, out CommandLineOptions options, out _));
		Assert.IsTrue(options.TrayRequested);
	}

	[TestMethod]
	public void TryParse_WithNoTray_SetsTraySuppressed()
	{
		Assert.IsTrue(CommandLineParser.TryParse(["--no-tray"], NoOptions, out CommandLineOptions options, out _));
		Assert.IsTrue(options.TraySuppressed);
	}

	[TestMethod]
	public void TryParse_WithBothTraySwitches_Fails()
	{
		Assert.IsFalse(CommandLineParser.TryParse(["--tray", "--no-tray"], NoOptions, out _, out string error));
		Assert.Contains("contradict", error, StringComparison.Ordinal);
	}

	[TestMethod]
	public void TryParse_WithDuration_ParsesIt()
	{
		Assert.IsTrue(CommandLineParser.TryParse(["--for", "90m"], NoOptions, out CommandLineOptions options, out _));
		Assert.AreEqual(TimeSpan.FromMinutes(90), options.Duration);
	}

	[TestMethod]
	public void TryParse_WithAnUnreadableDuration_Fails()
	{
		Assert.IsFalse(CommandLineParser.TryParse(["--for", "soon"], NoOptions, out _, out string error));
		Assert.Contains("is not a duration", error, StringComparison.Ordinal);
	}

	[TestMethod]
	public void TryParse_WithADurationAndNoValue_Fails()
	{
		Assert.IsFalse(CommandLineParser.TryParse(["--for"], NoOptions, out _, out string error));
		Assert.Contains("needs a value", error, StringComparison.Ordinal);
	}

	[TestMethod]
	[DataRow("-s", nameof(CommandLineOptions.ShowStatus))]
	[DataRow("--status", nameof(CommandLineOptions.ShowStatus))]
	[DataRow("-h", nameof(CommandLineOptions.ShowHelp))]
	[DataRow("--help", nameof(CommandLineOptions.ShowHelp))]
	[DataRow("-?", nameof(CommandLineOptions.ShowHelp))]
	[DataRow("-v", nameof(CommandLineOptions.ShowVersion))]
	[DataRow("--version", nameof(CommandLineOptions.ShowVersion))]
	public void TryParse_WithAReportingSwitch_SetsIt(string argument, string expected)
	{
		Assert.IsTrue(CommandLineParser.TryParse([argument], NoOptions, out CommandLineOptions options, out _));

		bool actual = expected switch
		{
			nameof(CommandLineOptions.ShowStatus) => options.ShowStatus,
			nameof(CommandLineOptions.ShowHelp) => options.ShowHelp,
			_ => options.ShowVersion,
		};

		Assert.IsTrue(actual);
	}

	[TestMethod]
	public void TryParse_WithAnUnknownOption_Fails()
	{
		Assert.IsFalse(CommandLineParser.TryParse(["--nope"], NoOptions, out _, out string error));
		Assert.Contains("Unknown option", error, StringComparison.Ordinal);
	}

	[TestMethod]
	public void TryParse_WithAPositionalArgument_Fails()
	{
		Assert.IsFalse(CommandLineParser.TryParse(["thing"], NoOptions, out _, out string error));
		Assert.Contains("Unexpected argument", error, StringComparison.Ordinal);
	}

	[TestMethod]
	public void TryParse_WithAnApplicationFlag_RecordsItWithoutRunningIt()
	{
		bool ran = false;
		AppOption display = AppOption.Flag(["-d", "--display"], "Keep the display lit.", () => ran = true);

		Assert.IsTrue(CommandLineParser.TryParse(["--display"], [display], out CommandLineOptions options, out _));

		// The parser records matches and the builder applies them, so that a remembered preference can be
		// restored first and still lose to what was typed this time.
		Assert.IsFalse(ran);
		Assert.AreEqual(1, options.Matches.Count);
		Assert.AreSame(display, options.Matches[0].Option);
		Assert.IsNull(options.Matches[0].Value);

		options.Matches[0].Option.Apply(options.Matches[0].Value);
		Assert.IsTrue(ran);
	}

	[TestMethod]
	public void TryParse_WithAnApplicationValueOption_TakesTheNextArgument()
	{
		string? captured = null;
		AppOption reason = AppOption.Value(["-r", "--reason"], "text", "Why.", value => captured = value);

		Assert.IsTrue(CommandLineParser.TryParse(["--reason", "a long build"], [reason], out CommandLineOptions options, out _));

		Assert.AreEqual(1, options.Matches.Count);
		Assert.AreEqual("a long build", options.Matches[0].Value);

		options.Matches[0].Option.Apply(options.Matches[0].Value);
		Assert.AreEqual("a long build", captured);
	}

	[TestMethod]
	public void TryParse_WithAnApplicationValueOptionAndNoValue_Fails()
	{
		AppOption reason = AppOption.Value(["-r", "--reason"], "text", "Why.", _ => { });

		Assert.IsFalse(CommandLineParser.TryParse(["--reason"], [reason], out _, out string error));
		Assert.Contains("needs a value", error, StringComparison.Ordinal);
	}

	[TestMethod]
	public void TryParse_WithEverything_KeepsTheOrderOfApplicationOptions()
	{
		AppOption first = AppOption.Flag(["--one"], "One.", () => { });
		AppOption second = AppOption.Flag(["--two"], "Two.", () => { });

		Assert.IsTrue(CommandLineParser.TryParse(
			["--two", "--tray", "--one", "--for", "5s"],
			[first, second],
			out CommandLineOptions options,
			out _));

		Assert.IsTrue(options.TrayRequested);
		Assert.AreEqual(TimeSpan.FromSeconds(5), options.Duration);
		Assert.AreEqual(2, options.Matches.Count);
		Assert.AreSame(second, options.Matches[0].Option);
		Assert.AreSame(first, options.Matches[1].Option);
	}
}
