// Copyright (c) 2026 ktsu-dev contributors

namespace ktsu.TrayApp;

using System;
using System.Collections.Generic;
using ktsu.Essentials;
using ktsu.TrayApp.Cli;
using ktsu.TrayApp.Menu;

/// <summary>
/// The finished description of a tray tool: everything <see cref="TrayAppBuilder"/> was told, frozen.
/// </summary>
/// <remarks>
/// Internal on purpose. The builder is the API; this is the shape the runner, the console mode, and the
/// status report read, and pinning it in public would make every one of those an API decision.
/// </remarks>
internal sealed class TrayAppDefinition
{
	/// <summary>Gets the command name, as it is typed.</summary>
	public required string Name { get; init; }

	/// <summary>Gets the name shown in the menu and in messages.</summary>
	public required string DisplayName { get; init; }

	/// <summary>Gets the sentence under the usage line, or <see langword="null"/>.</summary>
	public string? Summary { get; init; }

	/// <summary>Gets what the tool is doing, for the menu's status line and for <c>--status</c>.</summary>
	public Func<string>? Status { get; init; }

	/// <summary>Gets extra label/value rows for <c>--status</c>.</summary>
	public IReadOnlyList<StatusDetail> StatusDetails { get; init; } = [];

	/// <summary>Gets the tool's own menu entries, in the order they were registered.</summary>
	public IReadOnlyList<TrayMenuItem> Items { get; init; } = [];

	/// <summary>Gets the toggles among <see cref="Items"/>, for preference restore.</summary>
	public IReadOnlyList<TrayToggleItem> Toggles { get; init; } = [];

	/// <summary>Gets the application-specific command line options.</summary>
	public IReadOnlyList<AppOption> Options { get; init; } = [];

	/// <summary>Gets the tray images, or <see langword="null"/> when the tool registered none.</summary>
	public TrayIconSet? Icons { get; init; }

	/// <summary>Gets the predicate that picks between the active and idle images.</summary>
	public Func<bool>? IsActive { get; init; }

	/// <summary>Gets what to run when the tool starts, in either mode.</summary>
	public Action? OnStart { get; init; }

	/// <summary>Gets what to run on the way out, in either mode.</summary>
	public Action? OnStop { get; init; }

	/// <summary>
	/// Gets the tool's hook for telling the tray to re-read the menu, or <see langword="null"/> when the tool
	/// only ever changes state from the menu itself.
	/// </summary>
	public Action<Action>? RefreshSubscription { get; init; }

	/// <summary>Gets where remembered toggle states live, or <see langword="null"/> when nothing is persisted.</summary>
	public IPersistenceProvider<string>? Persistence { get; init; }

	/// <summary>Gets the key the remembered state is stored under.</summary>
	public required string PreferenceKey { get; init; }

	/// <summary>Gets how long a burst of preference changes is coalesced for before it is written.</summary>
	public TimeSpan PreferenceDebounce { get; init; }

	/// <summary>Describes what the tool is doing, or a neutral sentence when it said nothing.</summary>
	/// <returns>The description.</returns>
	public string Describe() => Status?.Invoke() ?? $"{DisplayName} is running";
}
