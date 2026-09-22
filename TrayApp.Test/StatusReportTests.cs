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
