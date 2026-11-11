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
	private const int MinimumGap = 1;

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

		List<(string Label, string Value)> rows =
		[
			("Platform", string.Create(CultureInfo.InvariantCulture, $"{RuntimeInformation.OSDescription.Trim()} ({RuntimeInformation.RuntimeIdentifier})")),
			("Tray icon", DesktopSession.Explanation),
		];

		if (status is not null)
		{
			rows.Add(("State", status));
		}

		foreach (StatusDetail detail in details)
		{
			rows.Add((detail.Label, Read(detail.Value)));
		}

		// The value column starts after the longest label, so a long label still has a gap before its value
		// and every value lines up with the others.
		int labelWidth = LabelWidth;
		foreach ((string label, _) in rows)
		{
			labelWidth = Math.Max(labelWidth, label.Length + MinimumGap + 1);
		}

		StringBuilder report = new();
		report.AppendLine(HelpText.Version(name));
		string padding = new(' ', labelWidth);
		foreach ((string label, string value) in rows)
		{
			// A value can span lines (an exception message, a list of helpers). Every line after the first is
			// indented to the value column, as --help does, so it doesn't read as a row of its own.
			string[] lines = value.Split('\n');
			report.AppendLine($"{(label + ':').PadRight(labelWidth)}{lines[0].TrimEnd('\r')}");
			foreach (string line in lines[1..])
			{
				report.AppendLine($"{padding}{line.TrimEnd('\r')}");
			}
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
}
