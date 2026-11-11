// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.TrayApp.Preferences;

using ktsu.TrayApp.Menu;

/// <summary>
/// A toggle that a preference restore changed, and the state it was in before.
/// </summary>
/// <param name="Toggle">The toggle the restore set.</param>
/// <param name="Before">The state the toggle reported before the restore set it.</param>
/// <remarks>
/// Remembered preferences are the tray's. When the tray then fails to come up and the run falls back to the
/// console, these are what put the toggles back, so the console run sees what the command line asked for.
/// </remarks>
internal sealed record RestoredToggle(TrayToggleItem Toggle, bool Before);
