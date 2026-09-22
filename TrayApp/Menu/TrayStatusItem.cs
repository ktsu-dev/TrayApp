// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.TrayApp.Menu;

using System;

/// <summary>
/// The disabled line at the top of the menu that says what the tool is currently doing.
/// </summary>
/// <remarks>
/// A tray app has no console to report into and no window to raise a dialog over, so the one place a user
/// is already looking has to carry both the ordinary state and the last failure. The text comes from
/// <see cref="TrayMenu.Refresh"/> rather than from a getter on this item, because the menu is what knows
/// whether the last action failed.
/// </remarks>
public sealed class TrayStatusItem : TrayMenuItem
{
	private readonly Func<string> describe;

	internal TrayStatusItem(Func<string> describe)
	{
		Ensure.NotNull(describe);

		this.describe = describe;
		IsEnabled = false;
		Header = string.Empty;
	}

	/// <inheritdoc/>
	public override void Refresh()
	{
		Header = describe();
		IsEnabled = false;
	}
}
