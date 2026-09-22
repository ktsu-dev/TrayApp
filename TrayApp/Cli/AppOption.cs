// Copyright (c) 2026 ktsu-dev contributors

namespace ktsu.TrayApp.Cli;

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// One command line switch a tool adds on top of the standard set.
/// </summary>
/// <remarks>
/// The standard flags - <c>--tray</c>, <c>--no-tray</c>, <c>--for</c>, <c>--status</c>, <c>--help</c> and
/// <c>--version</c> - are the library's, and everything a particular tool needs beyond them arrives as one
/// of these. They appear in <c>--help</c> in registration order, between the tray switches and
/// <c>--for</c>.
/// </remarks>
public sealed class AppOption
{
	private readonly Action? onSet;
	private readonly Action<string>? onValue;

	private AppOption(IReadOnlyList<string> aliases, string? valueName, string description, Action? onSet, Action<string>? onValue)
	{
		Ensure.NotNull(aliases);
		Ensure.NotNull(description);

		if (aliases.Count == 0)
		{
			throw new ArgumentException("An option needs at least one alias.", nameof(aliases));
		}

		Aliases = [.. aliases];
		ValueName = valueName;
		Description = description;
		this.onSet = onSet;
		this.onValue = onValue;
	}

	/// <summary>
	/// Gets the spellings that select this option, such as <c>-d</c> and <c>--display</c>.
	/// </summary>
	public IReadOnlyList<string> Aliases { get; }

	/// <summary>
	/// Gets the placeholder shown for the option's value in <c>--help</c>, or <see langword="null"/> when the
	/// option is a switch that takes no value.
	/// </summary>
	public string? ValueName { get; }

	/// <summary>
	/// Gets the one-line description shown in <c>--help</c>.
	/// </summary>
	public string Description { get; }

	/// <summary>
	/// Gets a value indicating whether this option consumes the argument that follows it.
	/// </summary>
	public bool TakesValue => ValueName is not null;

	/// <summary>
	/// Creates a switch that takes no value.
	/// </summary>
	/// <param name="aliases">The spellings that select it, longest-lived first; <c>["-d", "--display"]</c>.</param>
	/// <param name="description">The one-line description shown in <c>--help</c>.</param>
	/// <param name="onSet">Called once when the switch is present, however many times it was given.</param>
	/// <returns>The option.</returns>
	/// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException"><paramref name="aliases"/> is empty.</exception>
	public static AppOption Flag(IReadOnlyList<string> aliases, string description, Action onSet)
	{
		Ensure.NotNull(onSet);
		return new AppOption(aliases, valueName: null, description, onSet, onValue: null);
	}

	/// <summary>
	/// Creates an option that consumes the argument after it.
	/// </summary>
	/// <param name="aliases">The spellings that select it; <c>["-r", "--reason"]</c>.</param>
	/// <param name="valueName">The placeholder shown in <c>--help</c>, such as <c>text</c>.</param>
	/// <param name="description">The one-line description shown in <c>--help</c>.</param>
	/// <param name="onValue">
	/// Called with the value. Throw <see cref="FormatException"/> or <see cref="ArgumentException"/> from
	/// here to reject a value: the message becomes the usage error, and the tool exits 2 without starting.
	/// </param>
	/// <returns>The option.</returns>
	/// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException"><paramref name="aliases"/> is empty.</exception>
	public static AppOption Value(IReadOnlyList<string> aliases, string valueName, string description, Action<string> onValue)
	{
		Ensure.NotNull(valueName);
		Ensure.NotNull(onValue);
		return new AppOption(aliases, valueName, description, onSet: null, onValue);
	}

	/// <summary>
	/// Determines whether an argument selects this option.
	/// </summary>
	/// <param name="argument">The argument as it was typed.</param>
	/// <returns><see langword="true"/> when <paramref name="argument"/> is one of <see cref="Aliases"/>.</returns>
	public bool Matches(string argument) => Aliases.Contains(argument, StringComparer.Ordinal);

	/// <summary>
	/// Runs the handler this option was created with.
	/// </summary>
	/// <param name="value">The value read from the command line, or <see langword="null"/> for a switch.</param>
	/// <remarks>
	/// Handlers are run by <see cref="TrayAppBuilder"/> rather than by the parser, and deliberately after any
	/// remembered preferences have been applied: what someone typed this time has to beat what the tray was
	/// left set to last time.
	/// </remarks>
	public void Apply(string? value)
	{
		if (TakesValue)
		{
			onValue!(value ?? string.Empty);
			return;
		}

		onSet!();
	}
}
