// Copyright (c) 2026 ktsu-dev contributors

namespace ktsu.TrayApp.Menu;

using System;

/// <summary>
/// A checkable menu entry backed by a getter and a setter on the tool.
/// </summary>
/// <remarks>
/// The item owns no state of its own. <see cref="IsChecked"/> is a cache of what the getter last said, so a
/// toggle the application flipped by itself - a timer expiring, a device disappearing - shows up on the next
/// refresh without the application having to tell the menu about it.
/// </remarks>
public sealed class TrayToggleItem : TrayMenuItem
{
	private readonly Func<bool> getState;
	private readonly Action<bool> setState;
	private readonly Func<bool>? getEnabled;

	internal TrayToggleItem(string header, Func<bool> getState, Action<bool> setState, Func<bool>? getEnabled, string? preferenceKey)
	{
		Ensure.NotNull(header);
		Ensure.NotNull(getState);
		Ensure.NotNull(setState);

		Header = header;
		this.getState = getState;
		this.setState = setState;
		this.getEnabled = getEnabled;
		PreferenceKey = preferenceKey;
	}

	/// <summary>
	/// Gets a value indicating whether the toggle was on when it was last refreshed.
	/// </summary>
	public bool IsChecked { get; private set; }

	/// <summary>
	/// Gets the name this toggle is remembered under between runs, or <see langword="null"/> when it is not
	/// persisted.
	/// </summary>
	/// <remarks>
	/// The key is given explicitly rather than derived from <see cref="TrayMenuItem.Header"/> so that
	/// rewording a menu entry does not quietly forget what the user had set.
	/// </remarks>
	public string? PreferenceKey { get; }

	/// <inheritdoc/>
	public override void Refresh()
	{
		IsChecked = getState();
		IsEnabled = getEnabled?.Invoke() ?? true;
	}

	/// <summary>
	/// Sets the toggle to a value.
	/// </summary>
	/// <param name="value">The value to set.</param>
	/// <remarks>
	/// This runs the tool's setter, which may throw. Going through <see cref="TrayMenu.Activate"/> instead is
	/// what turns such a refusal into a line in the menu rather than an exception out of a click handler.
	/// </remarks>
	public void Set(bool value) => setState(value);

	/// <summary>
	/// Sets the toggle to the opposite of what the tool currently reports.
	/// </summary>
	/// <remarks>
	/// The current value is read from the getter rather than from <see cref="IsChecked"/>, so a click acts on
	/// what is true now rather than on what the menu was last painted with.
	/// </remarks>
	public void Toggle() => setState(!getState());
}
