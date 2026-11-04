// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.TrayApp.Test;

using System;
using System.Collections.Generic;
using ktsu.TrayApp.Cli;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests for the generated usage text.
/// </summary>
[TestClass]
public class HelpTextTests
{
	private static readonly IReadOnlyList<AppOption> NoOptions = [];

	[TestMethod]
	public void Usage_ListsTheStandardOptions()
	{
		string usage = HelpText.Usage("demo", "does a thing.", NoOptions);

		Assert.Contains("demo - does a thing.", usage, StringComparison.Ordinal);
		Assert.Contains("--tray", usage, StringComparison.Ordinal);
		Assert.Contains("--no-tray", usage, StringComparison.Ordinal);
		Assert.Contains("--for <duration>", usage, StringComparison.Ordinal);
		Assert.Contains("--status", usage, StringComparison.Ordinal);
		Assert.Contains("--help", usage, StringComparison.Ordinal);
		Assert.Contains("--version", usage, StringComparison.Ordinal);
		Assert.Contains("A bare number means minutes.", usage, StringComparison.Ordinal);
	}

	[TestMethod]
	public void Usage_ListsTheToolsOwnOptionsBetweenTheTraySwitchesAndFor()
	{
		AppOption display = AppOption.Flag(["-d", "--display"], "Keep the display lit as well.", () => { });
		AppOption reason = AppOption.Value(["--reason"], "text", "Why the tool is running.", _ => { });

		string usage = HelpText.Usage("demo", summary: null, [display, reason]);

		int noTray = usage.IndexOf("--no-tray", StringComparison.Ordinal);
		int displayed = usage.IndexOf("-d, --display", StringComparison.Ordinal);
		int reasoned = usage.IndexOf("--reason <text>", StringComparison.Ordinal);
		int forIndex = usage.IndexOf("--for <duration>", StringComparison.Ordinal);

		Assert.IsGreaterThan(noTray, displayed);
		Assert.IsGreaterThan(displayed, reasoned);
		Assert.IsGreaterThan(reasoned, forIndex);
	}

	[TestMethod]
	[DataRow("duration", DisplayName = "23-character row, which fits the default column")]
	[DataRow("explanation", DisplayName = "26-character row, which widens the column")]
	[DataRow("a-much-longer-name", DisplayName = "33-character row, which wraps")]
	public void Usage_LeavesAGapAfterTheLongestOption(string valueName)
	{
		AppOption reason = AppOption.Value(["-r", "--reason"], valueName, "Why it is held.", _ => { });

		string usage = HelpText.Usage("demo", summary: null, [reason]);

		string columns = $"-r, --reason <{valueName}>";
		string row = FindLine(usage, columns);
		string rest = row[(row.IndexOf(columns, StringComparison.Ordinal) + columns.Length)..];

		if (rest.Length == 0)
		{
			// Wrapped: the description is on the next line, at the widest description column.
			string[] lines = usage.Split(Environment.NewLine);
			string next = lines[Array.IndexOf(lines, row) + 1];
			Assert.AreEqual(new string(' ', 34) + "Why it is held.", next);
		}
		else
		{
			Assert.StartsWith("  ", rest, StringComparison.Ordinal);
			Assert.AreEqual("Why it is held.", rest.TrimStart());
		}
	}

	[TestMethod]
	public void Usage_KeepsTheBuiltInRowsAtTheDefaultColumn()
	{
		string usage = HelpText.Usage("demo", summary: null, NoOptions);

		Assert.AreEqual("  -f, --for <duration>   Stop and exit after a duration: 45s, 90m, 2h, 1h30m.", FindLine(usage, "--for <duration>"));
		Assert.AreEqual("                         A bare number means minutes.", FindLine(usage, "A bare number"));
		Assert.AreEqual("  -h, --help             Show this help, then exit.", FindLine(usage, "--help"));
	}

	[TestMethod]
	public void Version_UsesTheAssemblyItIsGiven()
	{
		string version = HelpText.Version("demo", typeof(HelpTextTests).Assembly);

		Assert.StartsWith("demo ", version, StringComparison.Ordinal);

		// The SDK appends the source revision after a '+', which is noise in a version line.
		Assert.DoesNotContain("+", version, StringComparison.Ordinal);
	}

	private static string FindLine(string usage, string contains)
	{
		foreach (string line in usage.Split(Environment.NewLine))
		{
			if (line.Contains(contains, StringComparison.Ordinal))
			{
				return line;
			}
		}

		Assert.Fail($"No line contains '{contains}':{Environment.NewLine}{usage}");
		return string.Empty;
	}
}
