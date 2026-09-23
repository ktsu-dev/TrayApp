// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.TrayApp.Menu;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;

/// <summary>
/// The whole tray menu: the tool's own entries, a status line above them, and a quit entry below.
/// </summary>
/// <remarks>
/// Quit is appended here rather than left to each tool, because a tray-only process with no quit entry is a
/// process a user cannot stop without a task manager.
/// </remarks>
public sealed class TrayMenu
{
	private readonly string displayName;

	/// <summary>
	/// Initializes a new instance of the <see cref="TrayMenu"/> class.
	/// </summary>
	/// <param name="displayName">The tool's name, used in the quit entry and in failure messages.</param>
	/// <param name="status">
	/// Describes what the tool is doing, for the disabled line at the top of the menu, or
	/// <see langword="null"/> for a menu with no status line.
	/// </param>
	/// <param name="items">The tool's own entries, in the order they should appear.</param>
	/// <exception cref="ArgumentNullException"><paramref name="displayName"/> or <paramref name="items"/> is <see langword="null"/>.</exception>
	public TrayMenu(string displayName, Func<string>? status, IEnumerable<TrayMenuItem> items)
	{
		Ensure.NotNull(displayName);
		Ensure.NotNull(items);

		this.displayName = displayName;

		List<TrayMenuItem> all = [];

		if (status is not null)
		{
			StatusItem = new TrayStatusItem(() => LastError is null ? status() : $"{displayName} failed: {LastError}");
			all.Add(StatusItem);
			all.Add(new TraySeparatorItem());
		}

		all.AddRange(items);

		QuitItem = new TrayCommandItem($"Quit {displayName}", () => QuitRequested?.Invoke(this, EventArgs.Empty), getEnabled: null);
		all.Add(new TraySeparatorItem());
		all.Add(QuitItem);

		Items = new ReadOnlyCollection<TrayMenuItem>(all);
	}

	/// <summary>
	/// Raised when the quit entry is clicked.
	/// </summary>
	/// <remarks>
	/// The menu model knows nothing about how the process ends, so ending it is the host's job.
	/// </remarks>
	public event EventHandler? QuitRequested;

	/// <summary>
	/// Gets every entry in the menu, including the status line, the separators, and quit.
	/// </summary>
	public IReadOnlyList<TrayMenuItem> Items { get; }

	/// <summary>
	/// Gets the disabled line at the top of the menu, or <see langword="null"/> when the menu has none.
	/// </summary>
	public TrayStatusItem? StatusItem { get; }

	/// <summary>
	/// Gets the quit entry.
	/// </summary>
	public TrayCommandItem QuitItem { get; }

	/// <summary>
	/// Gets the message from the last action that failed, or <see langword="null"/> when the last action
	/// succeeded.
	/// </summary>
	/// <remarks>
	/// Kept here rather than written straight into the status item, because <see cref="Activate"/> refreshes
	/// the menu immediately afterwards and that refresh would overwrite anything written directly.
	/// </remarks>
	public string? LastError { get; private set; }

	/// <summary>
	/// Records a failure that happened before the menu existed.
	/// </summary>
	/// <param name="message">The message, or <see langword="null"/> to clear it.</param>
	/// <remarks>
	/// Restoring a remembered toggle runs the tool's setter before there is a menu to fail into, and the
	/// next successful action clears this the same way it clears a failed click.
	/// </remarks>
	internal void SeedError(string? message) => LastError = message;

	/// <summary>
	/// Re-reads every item's state from the tool.
	/// </summary>
	public void Refresh()
	{
		foreach (TrayMenuItem item in Items)
		{
			item.Refresh();
		}
	}

	/// <summary>
	/// Clicks an item, records any failure, and refreshes the menu.
	/// </summary>
	/// <param name="item">The item to activate.</param>
	/// <returns><see langword="true"/> when the item's action completed without throwing.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="item"/> is <see langword="null"/>.</exception>
	/// <remarks>
	/// A refusal by the tool - a device that went away, a permission that was revoked - is not the menu's to
	/// interpret, and there is nowhere for it to go from a click handler: an exception out of one takes the
	/// tray icon down with it. It is written to standard error for anyone who started the tool from a
	/// terminal, and shown in the status line for everyone else.
	/// </remarks>
	public bool Activate(TrayMenuItem item)
	{
		Ensure.NotNull(item);

		return Run(() =>
		{
			switch (item)
			{
				case TrayToggleItem toggle:
					toggle.Toggle();
					break;

				case TrayCommandItem command:
					command.Invoke();
					break;

				default:
					break;
			}
		});
	}

	/// <summary>
	/// Runs one of the tool's actions under the same failure handling as a click, and refreshes the menu.
	/// </summary>
	/// <param name="action">The action to run.</param>
	/// <returns><see langword="true"/> when the action completed without throwing.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="action"/> is <see langword="null"/>.</exception>
	/// <remarks>
	/// The host uses this for the work that is not a click but fails the same way and has the same nowhere to
	/// report it: the tool's start action, and restoring a remembered toggle.
	/// </remarks>
	[SuppressMessage(
		"Design",
		"CA1031:Do not catch general exception types",
		Justification = "The action belongs to the consuming tool, so there is no exception type to filter on beyond the fatal ones FatalError excludes, and letting one escape a tray click handler takes down a process the user asked to keep running.")]
	public bool Run(Action action)
	{
		Ensure.NotNull(action);

		bool succeeded = true;

		try
		{
			action();
			LastError = null;
		}
		catch (Exception ex) when (FatalError.IsNotFatal(ex))
		{
			LastError = ex.Message;
			Console.Error.WriteLine($"{displayName}: {ex.Message}");
			succeeded = false;
		}

		Refresh();
		return succeeded;
	}
}
