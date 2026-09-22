// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.TrayApp.Menu;

/// <summary>
/// A horizontal rule between groups of menu entries.
/// </summary>
public sealed class TraySeparatorItem : TrayMenuItem
{
	/// <summary>
	/// Initializes a new instance of the <see cref="TraySeparatorItem"/> class.
	/// </summary>
	public TraySeparatorItem() => IsEnabled = false;

	/// <inheritdoc/>
	public override void Refresh()
	{
		// A separator shows nothing and reads nothing.
	}
}
