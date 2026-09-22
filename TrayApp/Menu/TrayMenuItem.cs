// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.TrayApp.Menu;

/// <summary>
/// One entry in a tray menu, independent of any windowing toolkit.
/// </summary>
/// <remarks>
/// The model is deliberately free of Avalonia: the host projects it onto native menu items, and everything
/// worth asserting about a menu - what it says, what is ticked, what is greyed out after a refresh - can be
/// tested on a build agent with no display.
/// </remarks>
public abstract class TrayMenuItem
{
	/// <summary>
	/// Gets the text shown for this item.
	/// </summary>
	public string Header { get; internal set; } = string.Empty;

	/// <summary>
	/// Gets a value indicating whether this item can be clicked.
	/// </summary>
	public bool IsEnabled { get; internal set; } = true;

	/// <summary>
	/// Re-reads whatever state this item shows from the tool that owns it.
	/// </summary>
	/// <remarks>
	/// Every piece of state on a menu item comes from a getter rather than being pushed in, so one
	/// <see cref="TrayMenu.Refresh"/> brings the whole menu back in line with the application - however the
	/// application changed, and whether or not the menu is what changed it.
	/// </remarks>
	public abstract void Refresh();
}
