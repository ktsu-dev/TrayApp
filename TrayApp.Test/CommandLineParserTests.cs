// Copyright (c) 2023-2026 ktsu-dev contributors

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
	public void TryParse_WithADurationUnderATick_Fails()
	{
		Assert.IsFalse(CommandLineParser.TryParse(["--for", "0.00000001s"], NoOptions, out _, out string error));
		Assert.Contains("is not a duration", error, StringComparison.Ordinal);
	}

	[TestMethod]
	[DataRow("25d")]
	[DataRow("20000000d")]
	public void TryParse_WithADurationLongerThanATimerCanWait_FailsWithAUsageError(string text)
	{
		Assert.IsFalse(CommandLineParser.TryParse(["--for", text], NoOptions, out _, out string error));
		Assert.Contains("--for can be at most 24 days", error, StringComparison.Ordinal);
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
	public void TryParse_WithAFlagGivenUnderSeveralAliases_RunsItsHandlerOnce()
	{
		int calls = 0;
		AppOption display = AppOption.Flag(["-d", "--display"], "Keep the display lit.", () => calls++);

		Assert.IsTrue(CommandLineParser.TryParse(["-d", "--display", "-d"], [display], out CommandLineOptions options, out _));
		Assert.IsTrue(TrayAppBuilder.TryApplyOptions(options, out _));

		// A handler that toggles would otherwise be flipped back by the second spelling.
		Assert.AreEqual(1, calls);
		Assert.AreEqual(1, options.Matches.Count);
	}

	[TestMethod]
	public void TryParse_WithAValueOptionGivenTwice_AppliesBothInOrder()
	{
		List<string> seen = [];
		AppOption reason = AppOption.Value(["-r", "--reason"], "text", "Why.", seen.Add);

		Assert.IsTrue(CommandLineParser.TryParse(["-r", "first", "--reason", "second"], [reason], out CommandLineOptions options, out _));
		Assert.IsTrue(TrayAppBuilder.TryApplyOptions(options, out _));

		CollectionAssert.AreEqual(new[] { "first", "second" }, seen);
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
	[DataRow("--display")]
	[DataRow("-d")]
	[DataRow("--status")]
	[DataRow("-t")]
	public void TryParse_WithAnApplicationValueOptionFollowedByASwitch_FailsInsteadOfTakingTheSwitchAsItsValue(string next)
	{
		AppOption reason = AppOption.Value(["-r", "--reason"], "text", "Why.", _ => { });
		AppOption display = AppOption.Flag(["-d", "--display"], "Keep the display on.", () => { });

		Assert.IsFalse(CommandLineParser.TryParse(["--reason", next], [reason, display], out _, out string error));
		Assert.AreEqual("'--reason' needs a value.", error);
	}

	[TestMethod]
	public void TryParse_WithADurationFollowedByASwitch_SaysTheValueIsMissing()
	{
		Assert.IsFalse(CommandLineParser.TryParse(["--for", "--status"], NoOptions, out _, out string error));
		Assert.AreEqual("'--for' needs a value.", error);
	}

	[TestMethod]
	[DataRow("-")]
	[DataRow("-5")]
	[DataRow("some text")]
	public void TryParse_WithAValueThatIsNotAKnownSwitch_TakesIt(string value)
	{
		AppOption reason = AppOption.Value(["-r", "--reason"], "text", "Why.", _ => { });

		Assert.IsTrue(CommandLineParser.TryParse(["--reason", value], [reason], out CommandLineOptions options, out _));
		Assert.AreEqual(1, options.Matches.Count);
		Assert.AreEqual(value, options.Matches[0].Value);
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

	[TestMethod]
	public void TryParse_WithAnAttachedDuration_ParsesIt()
	{
		Assert.IsTrue(CommandLineParser.TryParse(["--for=5s"], NoOptions, out CommandLineOptions options, out string error), error);
		Assert.AreEqual(TimeSpan.FromSeconds(5), options.Duration);
	}

	[TestMethod]
	public void TryParse_WithAnAttachedDurationLeftEmpty_FailsAsMissing()
	{
		Assert.IsFalse(CommandLineParser.TryParse(["--for="], NoOptions, out _, out string error));
		Assert.AreEqual("'--for' needs a value.", error);
	}

	[TestMethod]
	public void TryParse_WithAnAttachedUnreadableDuration_FailsAsTheSeparateFormDoes()
	{
		Assert.IsFalse(CommandLineParser.TryParse(["--for=soon"], NoOptions, out _, out string error));
		Assert.AreEqual("'soon' is not a duration. Try 45s, 90m, 2h, or 1h30m.", error);
	}

	[TestMethod]
	[DataRow("text", "text")]
	[DataRow("--display", "--display")]
	[DataRow("a=b", "a=b")]
	public void TryParse_WithAnAttachedApplicationValue_TakesEverythingAfterTheFirstEquals(string attached, string expected)
	{
		AppOption reason = AppOption.Value(["-r", "--reason"], "text", "Why.", _ => { });
		AppOption display = AppOption.Flag(["-d", "--display"], "Display.", () => { });

		Assert.IsTrue(CommandLineParser.TryParse([$"--reason={attached}"], [reason, display], out CommandLineOptions options, out string error), error);
		Assert.AreEqual(1, options.Matches.Count);
		Assert.AreSame(reason, options.Matches[0].Option);
		Assert.AreEqual(expected, options.Matches[0].Value);
	}

	[TestMethod]
	public void TryParse_WithAnAttachedApplicationValueLeftEmpty_FailsAsMissing()
	{
		AppOption reason = AppOption.Value(["-r", "--reason"], "text", "Why.", _ => { });

		Assert.IsFalse(CommandLineParser.TryParse(["--reason="], [reason], out _, out string error));
		Assert.AreEqual("'--reason' needs a value.", error);
	}

	[TestMethod]
	[DataRow("--tray=yes", "--tray")]
	[DataRow("--status=1", "--status")]
	[DataRow("--display=on", "--display")]
	public void TryParse_WithAValueAttachedToASwitch_Fails(string argument, string name)
	{
		AppOption display = AppOption.Flag(["-d", "--display"], "Display.", () => { });

		Assert.IsFalse(CommandLineParser.TryParse([argument], [display], out _, out string error));
		Assert.AreEqual($"'{name}' does not take a value.", error);
	}

	[TestMethod]
	public void TryParse_WithAnAttachedValueOnAnUnknownName_ReportsTheName()
	{
		Assert.IsFalse(CommandLineParser.TryParse(["--nope=1"], NoOptions, out _, out string error));
		Assert.AreEqual("Unknown option '--nope'. Run with --help for the list.", error);
	}

	[TestMethod]
	public void TryParse_WithAnApplicationAliasThatHoldsAnEquals_ReadsItAsTyped()
	{
		AppOption odd = AppOption.Flag(["--mode=fast"], "Fast.", () => { });

		Assert.IsTrue(CommandLineParser.TryParse(["--mode=fast"], [odd], out CommandLineOptions options, out string error), error);
		Assert.AreSame(odd, options.Matches[0].Option);
	}
}
