// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.TrayApp.Test;

using System;
using System.Collections.Generic;
using ktsu.TrayApp;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests for the text behind <c>--status</c> and for the session detection it reports.
/// </summary>
[TestClass]
public class StatusReportTests
{
	private static readonly IReadOnlyList<StatusDetail> NoDetails = [];

	[TestMethod]
	public void Build_AlwaysReportsThePlatformAndTheTray()
	{
		string report = StatusReport.Build("demo", status: null, NoDetails);

		Assert.StartsWith("demo ", report, StringComparison.Ordinal);
		Assert.Contains("Platform:", report, StringComparison.Ordinal);
		Assert.Contains("Tray icon:", report, StringComparison.Ordinal);

		// No status was given, so there is no row for it.
		Assert.DoesNotContain("State:", report, StringComparison.Ordinal);
	}

	[TestMethod]
	public void Build_WithAStatusAndDetails_AppendsThemInOrder()
	{
		string report = StatusReport.Build(
			"demo",
			"holding",
			[new StatusDetail("Mechanism", () => "systemd-inhibit"), new StatusDetail("Helper", () => "missing")]);

		Assert.Contains("State:", report, StringComparison.Ordinal);
		Assert.Contains("holding", report, StringComparison.Ordinal);

		int mechanism = report.IndexOf("Mechanism:", StringComparison.Ordinal);
		int helper = report.IndexOf("Helper:", StringComparison.Ordinal);

		Assert.IsGreaterThan(-1, mechanism);
		Assert.IsGreaterThan(mechanism, helper);
		Assert.Contains("systemd-inhibit", report, StringComparison.Ordinal);
	}

	[TestMethod]
	public void Build_WithALongDetailLabel_KeepsAGapAndAlignsEveryValue()
	{
		string report = StatusReport.Build(
			"demo",
			"on",
			[new StatusDetail("Power assertion", () => "held"), new StatusDetail("Inhibitor path", () => "/usr/bin/systemd-inhibit")]);

		Assert.Contains("Power assertion: ", report, StringComparison.Ordinal);
		Assert.Contains("Inhibitor path: ", report, StringComparison.Ordinal);

		string[] rows = report.Split('\n')[1..];
		int column = rows[0].IndexOf(':', StringComparison.Ordinal) + 1;
		while (rows[0][column] == ' ')
		{
			column++;
		}

		foreach (string row in rows)
		{
			string line = row.TrimEnd('\r');
			Assert.AreEqual(' ', line[column - 1], $"Row \"{line}\" has no gap before its value column.");
			Assert.AreNotEqual(' ', line[column], $"Row \"{line}\" does not start its value at column {column}.");
		}
	}

	[TestMethod]
	public void Build_WithAThrowingDetail_ReportsTheErrorInItsRowAndKeepsTheRest()
	{
		string report = StatusReport.Build(
			"demo",
			"holding",
			[
				new StatusDetail("Inhibitor", () => throw new InvalidOperationException("systemd-inhibit was not found on PATH")),
				new StatusDetail("Helper", () => "present"),
			]);

		Assert.Contains("Platform:", report, StringComparison.Ordinal);
		Assert.Contains("Tray icon:", report, StringComparison.Ordinal);
		Assert.Contains("Inhibitor:     error: systemd-inhibit was not found on PATH", report, StringComparison.Ordinal);
		Assert.Contains("Helper:", report, StringComparison.Ordinal);
		Assert.Contains("present", report, StringComparison.Ordinal);
	}

	[TestMethod]
	public void Read_WithAThrowingGetter_ReturnsTheError() =>
		Assert.AreEqual("error: boom", StatusReport.Read(() => throw new InvalidOperationException("boom")));

	[TestMethod]
	public void Build_EndsWithoutATrailingNewline()
	{
		string report = StatusReport.Build("demo", "holding", NoDetails);

		Assert.DoesNotEndWith("\n", report, StringComparison.Ordinal);
		Assert.DoesNotEndWith("\r", report, StringComparison.Ordinal);
	}

	[TestMethod]
	public void DesktopSession_OnLinux_FollowsTheDisplayVariables()
	{
		if (!OperatingSystem.IsLinux())
		{
			// Everywhere else there is always somewhere to put a tray icon, which the next test covers.
			Assert.Inconclusive("The display variables only decide this on Linux.");
			return;
		}

		string? display = Environment.GetEnvironmentVariable("DISPLAY");
		string? wayland = Environment.GetEnvironmentVariable("WAYLAND_DISPLAY");

		try
		{
			Environment.SetEnvironmentVariable("DISPLAY", null);
			Environment.SetEnvironmentVariable("WAYLAND_DISPLAY", null);

			Assert.IsFalse(DesktopSession.IsAvailable);
			Assert.Contains("unavailable", DesktopSession.Explanation, StringComparison.Ordinal);

			Environment.SetEnvironmentVariable("DISPLAY", ":0");

			Assert.IsTrue(DesktopSession.IsAvailable);
			Assert.Contains("available", DesktopSession.Explanation, StringComparison.Ordinal);
		}
		finally
		{
			Environment.SetEnvironmentVariable("DISPLAY", display);
			Environment.SetEnvironmentVariable("WAYLAND_DISPLAY", wayland);
		}
	}

	[TestMethod]
	public void DesktopSession_OffLinux_IsAlwaysAvailable()
	{
		if (OperatingSystem.IsLinux())
		{
			Assert.Inconclusive("Linux is covered by the test above.");
			return;
		}

		Assert.IsTrue(DesktopSession.IsAvailable);
		Assert.AreEqual("available", DesktopSession.Explanation);
	}
}
