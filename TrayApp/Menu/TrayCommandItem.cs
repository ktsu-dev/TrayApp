// Copyright (c) 2026 ktsu-dev contributors

namespace ktsu.TrayApp.Menu;

using System;

/// <summary>
/// A menu entry that runs an action when it is clicked.
/// </summary>
public sealed class TrayCommandItem : TrayMenuItem
{
	private readonly Action invoke;
	private readonly Func<bool>? getEnabled;

	internal TrayCommandItem(string header, Action invoke, Func<bool>? getEnabled)
	{
		Ensure.NotNull(header);
		Ensure.NotNull(invoke);

		Header = header;
		this.invoke = invoke;
		this.getEnabled = getEnabled;
	}

	/// <inheritdoc/>
	public override void Refresh() => IsEnabled = getEnabled?.Invoke() ?? true;

	/// <summary>
	/// Runs the action.
	/// </summary>
	/// <remarks>
	/// This runs the tool's own code, which may throw. Going through <see cref="TrayMenu.Activate"/> instead
	/// is what turns such a failure into a line in the menu rather than an exception out of a click handler.
	/// </remarks>
	public void Invoke() => invoke();
}
