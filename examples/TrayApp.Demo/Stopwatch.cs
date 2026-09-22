// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.TrayApp.Demo;

using System;
using System.Globalization;

/// <summary>
/// The demo's entire platform layer: a clock that runs while a switch is on.
/// </summary>
/// <remarks>
/// A real tool would hold a power assertion, a mount, or a socket here. The point of the demo is that
/// ktsu.TrayApp does not care which: it needs a getter, a setter, and a sentence.
/// </remarks>
internal sealed class Stopwatch
{
	private DateTimeOffset startedAt = DateTimeOffset.UtcNow;

	/// <summary>
	/// Gets a value indicating whether the stopwatch is running.
	/// </summary>
	/// <remarks>
	/// On by default, so a console run does something without a menu to switch it on. In the tray the
	/// remembered value is restored over this before anything starts.
	/// </remarks>
	public bool IsRunning { get; private set; } = true;

	/// <summary>Starts or stops the stopwatch.</summary>
	/// <param name="running">Whether it should be running.</param>
	public void SetRunning(bool running)
	{
		if (running && !IsRunning)
		{
			startedAt = DateTimeOffset.UtcNow;
		}

		IsRunning = running;
	}

	/// <summary>Anchors the clock at the moment the tool starts, whatever the switch was restored to.</summary>
	public void Begin() => startedAt = DateTimeOffset.UtcNow;

	/// <summary>Stops the stopwatch on the way out.</summary>
	public void Stop() => SetRunning(false);

	/// <summary>Describes what the stopwatch is doing, for the menu and for <c>--status</c>.</summary>
	/// <returns>The description.</returns>
	public string Describe()
	{
		if (!IsRunning)
		{
			return "idle";
		}

		TimeSpan elapsed = DateTimeOffset.UtcNow - startedAt;
		return string.Create(CultureInfo.InvariantCulture, $"running for {elapsed:hh\\:mm\\:ss}");
	}
}
