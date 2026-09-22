// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.TrayApp.Cli;

using System;
using System.Collections.Generic;

/// <summary>
/// What the user asked for on the command line.
/// </summary>
public sealed class CommandLineOptions
{
	/// <summary>
	/// Gets a value indicating whether the tray icon was explicitly asked for with <c>--tray</c>.
	/// </summary>
	/// <remarks>
	/// Distinct from "the tray is wanted": with neither switch given the tray is used when the machine has a
	/// desktop session to put it in, and this says the user overrode that.
	/// </remarks>
	public bool TrayRequested { get; init; }

	/// <summary>
	/// Gets a value indicating whether <c>--no-tray</c> was given.
	/// </summary>
	public bool TraySuppressed { get; init; }

	/// <summary>
	/// Gets how long to run before stopping and exiting, or <see langword="null"/> to run until stopped.
	/// </summary>
	public TimeSpan? Duration { get; init; }

	/// <summary>
	/// Gets a value indicating whether to print what the tool can do on this machine and exit.
	/// </summary>
	public bool ShowStatus { get; init; }

	/// <summary>
	/// Gets a value indicating whether to print usage and exit.
	/// </summary>
	public bool ShowHelp { get; init; }

	/// <summary>
	/// Gets a value indicating whether to print the version and exit.
	/// </summary>
	public bool ShowVersion { get; init; }

	/// <summary>
	/// Gets the application-specific options that were given, in the order they appeared.
	/// </summary>
	/// <remarks>
	/// The parser records the matches rather than running the handlers, so the caller can choose when they
	/// take effect. <see cref="TrayAppBuilder"/> runs them after any remembered preferences have been
	/// restored, which is what makes an explicit switch beat what the tray was left set to.
	/// </remarks>
	public IReadOnlyList<AppOptionMatch> Matches { get; init; } = [];
}
