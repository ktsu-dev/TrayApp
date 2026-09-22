// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.TrayApp.Cli;

using System;
using System.Collections.Generic;
using System.Linq;

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
	/// What reading one argument did.
	/// </summary>
	private enum ReadResult
	{
		/// <summary>The argument was not one of the standard options.</summary>
		NotRecognised,

		/// <summary>The argument was read, along with any value it takes.</summary>
		Read,

		/// <summary>The argument was recognised but its value was wrong.</summary>
		Failed,
	}

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

		ParseState state = new();

		for (int index = 0; index < args.Count; index++)
		{
			string argument = args[index];
			ReadResult result = ReadStandardOption(args, ref index, argument, state, out error);

			if (result == ReadResult.Failed || (result == ReadResult.NotRecognised
				&& !TryReadAppOption(args, ref index, argument, appOptions, state, out error)))
			{
				options = new CommandLineOptions();
				return false;
			}
		}

		if (state.TrayRequested && state.TraySuppressed)
		{
			options = new CommandLineOptions();
			error = "--tray and --no-tray contradict each other.";
			return false;
		}

		options = state.ToOptions();
		error = string.Empty;
		return true;
	}

	private static ReadResult ReadStandardOption(
		IReadOnlyList<string> args,
		ref int index,
		string argument,
		ParseState state,
		out string error)
	{
		error = string.Empty;

		switch (argument)
		{
			case "-t" or "--tray":
				state.TrayRequested = true;
				return ReadResult.Read;

			case "--no-tray":
				state.TraySuppressed = true;
				return ReadResult.Read;

			case "-s" or "--status":
				state.ShowStatus = true;
				return ReadResult.Read;

			case "-h" or "--help" or "-?":
				state.ShowHelp = true;
				return ReadResult.Read;

			case "-v" or "--version":
				state.ShowVersion = true;
				return ReadResult.Read;

			case "-f" or "--for":
				return ReadDuration(args, ref index, argument, state, out error);

			default:
				return ReadResult.NotRecognised;
		}
	}

	private static ReadResult ReadDuration(
		IReadOnlyList<string> args,
		ref int index,
		string argument,
		ParseState state,
		out string error)
	{
		if (!TryTakeValue(args, ref index, argument, out string text, out error))
		{
			return ReadResult.Failed;
		}

		if (!DurationParser.TryParse(text, out TimeSpan duration))
		{
			error = $"'{text}' is not a duration. Try 45s, 90m, 2h, or 1h30m.";
			return ReadResult.Failed;
		}

		state.Duration = duration;
		return ReadResult.Read;
	}

	private static bool TryReadAppOption(
		IReadOnlyList<string> args,
		ref int index,
		string argument,
		IReadOnlyList<AppOption> appOptions,
		ParseState state,
		out string error)
	{
		AppOption? matched = appOptions.FirstOrDefault(option => option.Matches(argument));

		if (matched is null)
		{
			error = argument.StartsWith('-')
				? $"Unknown option '{argument}'. Run with --help for the list."
				: $"Unexpected argument '{argument}'. This tool takes options only.";
			return false;
		}

		string? value = null;

		if (matched.TakesValue && !TryTakeValue(args, ref index, argument, out value, out error))
		{
			return false;
		}

		state.Matches.Add(new AppOptionMatch(matched, value));
		error = string.Empty;
		return true;
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

	/// <summary>
	/// What has been read so far, so that reading one argument is a small method rather than another arm of
	/// a loop that owns seven locals.
	/// </summary>
	private sealed class ParseState
	{
		public bool TrayRequested { get; set; }

		public bool TraySuppressed { get; set; }

		public bool ShowStatus { get; set; }

		public bool ShowHelp { get; set; }

		public bool ShowVersion { get; set; }

		public TimeSpan? Duration { get; set; }

		public List<AppOptionMatch> Matches { get; } = [];

		public CommandLineOptions ToOptions() => new()
		{
			TrayRequested = TrayRequested,
			TraySuppressed = TraySuppressed,
			Duration = Duration,
			ShowStatus = ShowStatus,
			ShowHelp = ShowHelp,
			ShowVersion = ShowVersion,
			Matches = Matches,
		};
	}
}
