// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.TrayApp.Cli;

using System;
using System.Collections.Generic;

/// <summary>
/// Turns a tray tool's arguments into <see cref="CommandLineOptions"/>.
/// </summary>
/// <remarks>
/// The standard surface is six switches with no subcommands, so it is parsed here rather than pulled in from
/// a parser library: a hand-written loop is smaller than the configuration one would take, and it keeps the
/// error messages in the same voice as the rest of the tool.
/// </remarks>
public static class CommandLineParser
{
	/// <summary>
	/// Parses the arguments.
	/// </summary>
	/// <param name="args">The raw arguments, as handed to <c>Main</c>.</param>
	/// <param name="appOptions">The application-specific options to recognise alongside the standard ones.</param>
	/// <param name="options">Receives the parsed options when parsing succeeds.</param>
	/// <param name="error">Receives a message describing the problem when parsing fails.</param>
	/// <returns><see langword="true"/> when the arguments were understood.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="args"/> or <paramref name="appOptions"/> is <see langword="null"/>.</exception>
	public static bool TryParse(
		IReadOnlyList<string> args,
		IReadOnlyList<AppOption> appOptions,
		out CommandLineOptions options,
		out string error)
	{
		Ensure.NotNull(args);
		Ensure.NotNull(appOptions);

		bool tray = false;
		bool noTray = false;
		bool status = false;
		bool help = false;
		bool version = false;
		TimeSpan? duration = null;
		List<AppOptionMatch> matches = [];

		for (int index = 0; index < args.Count; index++)
		{
			string argument = args[index];

			switch (argument)
			{
				case "-t" or "--tray":
					tray = true;
					continue;

				case "--no-tray":
					noTray = true;
					continue;

				case "-s" or "--status":
					status = true;
					continue;

				case "-h" or "--help" or "-?":
					help = true;
					continue;

				case "-v" or "--version":
					version = true;
					continue;

				case "-f" or "--for":
					if (!TryTakeValue(args, ref index, argument, out string durationText, out error))
					{
						options = new CommandLineOptions();
						return false;
					}

					if (!DurationParser.TryParse(durationText, out TimeSpan parsedDuration))
					{
						options = new CommandLineOptions();
						error = $"'{durationText}' is not a duration. Try 45s, 90m, 2h, or 1h30m.";
						return false;
					}

					duration = parsedDuration;
					continue;

				default:
					break;
			}

			AppOption? matched = FindOption(appOptions, argument);

			if (matched is null)
			{
				options = new CommandLineOptions();
				error = argument.StartsWith('-')
					? $"Unknown option '{argument}'. Run with --help for the list."
					: $"Unexpected argument '{argument}'. This tool takes options only.";
				return false;
			}

			string? value = null;

			if (matched.TakesValue && !TryTakeValue(args, ref index, argument, out value, out error))
			{
				options = new CommandLineOptions();
				return false;
			}

			matches.Add(new AppOptionMatch(matched, value));
		}

		if (tray && noTray)
		{
			options = new CommandLineOptions();
			error = "--tray and --no-tray contradict each other.";
			return false;
		}

		options = new CommandLineOptions
		{
			TrayRequested = tray,
			TraySuppressed = noTray,
			Duration = duration,
			ShowStatus = status,
			ShowHelp = help,
			ShowVersion = version,
			Matches = matches,
		};

		error = string.Empty;
		return true;
	}

	private static AppOption? FindOption(IReadOnlyList<AppOption> appOptions, string argument)
	{
		foreach (AppOption option in appOptions)
		{
			if (option.Matches(argument))
			{
				return option;
			}
		}

		return null;
	}

	private static bool TryTakeValue(IReadOnlyList<string> args, ref int index, string option, out string value, out string error)
	{
		if (index + 1 >= args.Count)
		{
			value = string.Empty;
			error = $"'{option}' needs a value.";
			return false;
		}

		index++;
		value = args[index];
		error = string.Empty;
		return true;
	}
}
