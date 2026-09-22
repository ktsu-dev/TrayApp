// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.TrayApp.Cli;

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

/// <summary>
/// Builds the usage and version text from the options a tool registered.
/// </summary>
public static class HelpText
{
	private const int MinimumDescriptionColumn = 25;
	private const int MaximumDescriptionColumn = 34;

	/// <summary>
	/// Builds the usage text printed for <c>--help</c> and for a bad command line.
	/// </summary>
	/// <param name="name">The command name, as it is typed.</param>
	/// <param name="summary">A sentence describing what the tool does, or <see langword="null"/> to omit it.</param>
	/// <param name="appOptions">The application-specific options, listed between the tray switches and <c>--for</c>.</param>
	/// <returns>The usage text, without a trailing newline.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="name"/> or <paramref name="appOptions"/> is <see langword="null"/>.</exception>
	public static string Usage(string name, string? summary, IReadOnlyList<AppOption> appOptions)
	{
		Ensure.NotNull(name);
		Ensure.NotNull(appOptions);

		List<(string Columns, string Description)> rows =
		[
			("-t, --tray", "Show the tray icon even if no desktop session was detected."),
			("    --no-tray", "Stay in the terminal; never show a tray icon."),
		];

		foreach (AppOption option in appOptions)
		{
			rows.Add((DescribeAliases(option), option.Description));
		}

		rows.Add(("-f, --for <duration>", "Stop and exit after a duration: 45s, 90m, 2h, 1h30m.\nA bare number means minutes."));
		rows.Add(("-s, --status", $"Print what {name} can do on this machine, then exit."));
		rows.Add(("-h, --help", "Show this help, then exit."));
		rows.Add(("-v, --version", "Print the version, then exit."));

		int column = MinimumDescriptionColumn;

		foreach ((string columns, _) in rows)
		{
			column = Math.Max(column, Math.Min(MaximumDescriptionColumn, columns.Length + 2));
		}

		StringBuilder usage = new();
		usage.AppendLine(summary is null ? name : $"{name} - {summary}");
		usage.AppendLine();
		usage.AppendLine("Usage:");
		usage.AppendLine($"  {name} [options]");
		usage.AppendLine();
		usage.AppendLine("Options:");

		foreach ((string columns, string description) in rows)
		{
			AppendRow(usage, columns, description, column);
		}

		usage.AppendLine();
		usage.Append("Exit codes: 0 success, 1 the platform refused, 2 the command line was wrong.");

		return usage.ToString();
	}

	/// <summary>
	/// Builds the version line printed for <c>--version</c>.
	/// </summary>
	/// <param name="name">The command name, as it is typed.</param>
	/// <param name="assembly">
	/// The assembly to read the version from, or <see langword="null"/> for the entry assembly - which is the
	/// tool's own, not this library's.
	/// </param>
	/// <returns>The version line.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
	public static string Version(string name, Assembly? assembly = null)
	{
		Ensure.NotNull(name);

		Assembly source = assembly ?? Assembly.GetEntryAssembly() ?? typeof(HelpText).Assembly;
		string version = source.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
			?? source.GetName().Version?.ToString()
			?? "unknown";

		// The SDK appends the source revision after a '+', which is noise in a version line.
		int plus = version.IndexOf('+', StringComparison.Ordinal);
		if (plus >= 0)
		{
			version = version[..plus];
		}

		return $"{name} {version}";
	}

	private static string DescribeAliases(AppOption option)
	{
		List<string> shortForms = [];
		List<string> longForms = [];

		foreach (string alias in option.Aliases)
		{
			if (alias.StartsWith("--", StringComparison.Ordinal))
			{
				longForms.Add(alias);
			}
			else
			{
				shortForms.Add(alias);
			}
		}

		// With no short form the long one is indented to sit under the others, which is the shape every
		// hand-written usage block in this family of tools already has.
		string joined = string.Join(", ", [.. shortForms, .. longForms]);
		string columns = shortForms.Count == 0 ? "    " + joined : joined;

		return option.TakesValue ? $"{columns} <{option.ValueName}>" : columns;
	}

	private static void AppendRow(StringBuilder usage, string columns, string description, int column)
	{
		string[] lines = description.Split('\n');
		string padding = new(' ', column);

		if (columns.Length + 2 > column)
		{
			// A long alias list gets its description on the next line rather than pushing every other
			// description across the terminal.
			usage.AppendLine($"  {columns}");
		}
		else
		{
			usage.AppendLine($"  {columns.PadRight(column - 2)}{lines[0]}");
			lines = lines[1..];
		}

		foreach (string line in lines)
		{
			usage.AppendLine($"  {padding[2..]}{line}");
		}
	}
}
