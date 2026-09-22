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
	public void Version_UsesTheAssemblyItIsGiven()
	{
		string version = HelpText.Version("demo", typeof(HelpTextTests).Assembly);

		Assert.StartsWith("demo ", version, StringComparison.Ordinal);

		// The SDK appends the source revision after a '+', which is noise in a version line.
		Assert.DoesNotContain("+", version, StringComparison.Ordinal);
	}
}
