// Copyright (c) 2026 ktsu-dev contributors

namespace ktsu.TrayApp.Preferences;

using System.Collections.Generic;

/// <summary>
/// The tray's remembered state: which toggles were on when the tool last exited.
/// </summary>
/// <remarks>
/// Only the tray reads this. A command line run is explicit about what it wants, so letting a remembered
/// setting override an argument would be surprising, and the console path never touches it.
/// </remarks>
internal sealed class TrayPreferences
{
	/// <summary>
	/// Gets or sets the remembered value of each persisted toggle, keyed by its preference key.
	/// </summary>
	/// <remarks>
	/// The setter exists for the serializer: reflection-based JSON does not populate a get-only collection
	/// property, so a get-only one would round-trip as empty and quietly forget everything.
	/// </remarks>
	public Dictionary<string, bool> Toggles { get; set; } = [];
}
