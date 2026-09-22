// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.TrayApp.Test;

using System;
using ktsu.TrayApp.Cli;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests for <see cref="DurationParser"/>.
/// </summary>
/// <remarks>
/// Two cases carry the design. A bare <c>--for 90</c> has to mean ninety minutes, which is the one reading
/// <see cref="TimeSpan.TryParse(string, out TimeSpan)"/> would never give it; and <c>5m5</c> has to be
/// rejected rather than read as ten minutes, because it is a typo and running for the wrong length of time
/// is a failure nobody sees until it is over.
/// </remarks>
[TestClass]
public class DurationParserTests
{
	[TestMethod]
	[DataRow("45s", 45)]
	[DataRow("45S", 45)]
	[DataRow("45sec", 45)]
	[DataRow("2m", 120)]
	[DataRow("2min", 120)]
	[DataRow("1h", 3600)]
	[DataRow("1hr", 3600)]
	[DataRow("1h30m", 5400)]
	[DataRow("1h30m15s", 5415)]
	[DataRow("1d", 86400)]
	[DataRow("0.5h", 1800)]
	[DataRow("  90m  ", 5400)]
	public void TryParse_WithAValidDuration_ReturnsTheDuration(string text, int expectedSeconds)
	{
		Assert.IsTrue(DurationParser.TryParse(text, out TimeSpan duration));
		Assert.AreEqual(TimeSpan.FromSeconds(expectedSeconds), duration);
	}

	[TestMethod]
	public void TryParse_WithABareNumber_ReadsItAsMinutes()
	{
		Assert.IsTrue(DurationParser.TryParse("90", out TimeSpan duration));
		Assert.AreEqual(TimeSpan.FromMinutes(90), duration);
	}

	[TestMethod]
	public void TryParse_WithABareNumberAfterAUnit_ReturnsFalse()
	{
		// "5m5" is a typo, not five minutes and five more: a bare number is only a duration when it is the
		// whole value.
		Assert.IsFalse(DurationParser.TryParse("5m5", out TimeSpan duration));
		Assert.AreEqual(TimeSpan.Zero, duration);
	}

	[TestMethod]
	[DataRow("")]
	[DataRow("   ")]
	[DataRow(null)]
	[DataRow("0")]
	[DataRow("0s")]
	[DataRow("-5m")]
	[DataRow("forever")]
	[DataRow("5x")]
	[DataRow("m5")]
	[DataRow("1e9h")]
	public void TryParse_WithAnInvalidDuration_ReturnsFalse(string? text)
	{
		Assert.IsFalse(DurationParser.TryParse(text, out TimeSpan duration));
		Assert.AreEqual(TimeSpan.Zero, duration);
	}
}
