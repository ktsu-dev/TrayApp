// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.TrayApp;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using ktsu.TrayApp.Cli;

/// <summary>
/// Builds the text behind <c>--status</c>.
/// </summary>
/// <remarks>
/// A tray tool fails quietly by nature - the icon is simply absent, or the thing it was holding is simply
/// not held - so it needs a way to say up front what it can and cannot do on the machine it is on.
/// </remarks>
public static class StatusReport
{
	private const int LabelWidth = 15;

	/// <summary>
	/// Describes what a tool can do on this machine.
	/// </summary>
	/// <param name="name">The command name, as it is typed.</param>
	/// <param name="status">What the tool is doing, or <see langword="null"/> to omit the line.</param>
	/// <param name="details">Extra label/value rows, appended in order.</param>
	/// <returns>A multi-line report, without a trailing newline.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="name"/> or <paramref name="details"/> is <see langword="null"/>.</exception>
	public static string Build(string name, string? status, IReadOnlyList<StatusDetail> details)
	{
		Ensure.NotNull(name);
		Ensure.NotNull(details);

		StringBuilder report = new();
		report.AppendLine(HelpText.Version(name));
		AppendRow(report, "Platform", string.Create(CultureInfo.InvariantCulture, $"{RuntimeInformation.OSDescription.Trim()} ({RuntimeInformation.RuntimeIdentifier})"));
		AppendRow(report, "Tray icon", DesktopSession.Explanation);

		if (status is not null)
		{
			AppendRow(report, "State", status);
		}

		foreach (StatusDetail detail in details)
		{
			AppendRow(report, detail.Label, Read(detail.Value));
		}

		// Every row above appended a newline; the report is quoted and compared in tests, so it ends where
		// the last row's text ends.
		return report.ToString().TrimEnd('\r', '\n');
	}

	/// <summary>
	/// Reads one of the tool's status getters for the report.
	/// </summary>
	/// <remarks>
	/// The report exists to explain a tool that cannot do its job, and a getter is likely to throw for the
	/// same reason (a missing helper, say), so a throwing getter becomes its own row's text rather than
	/// losing every row the library and the other getters would have printed.
	/// </remarks>
	/// <param name="getter">The tool's getter.</param>
	/// <returns>What the getter returned, or <c>error: </c> and the message of what it threw.</returns>
	internal static string Read(Func<string> getter)
	{
		try
		{
			return getter();
		}
		catch (Exception ex) when (FatalError.IsNotFatal(ex))
		{
			return "error: " + ex.Message;
		}
	}

	private static void AppendRow(StringBuilder report, string label, string value) =>
		report.AppendLine($"{(label + ':').PadRight(LabelWidth)}{value}");
}
