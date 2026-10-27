// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.TrayApp.Tray;

using System;
using System.Runtime.InteropServices;

/// <summary>
/// Turns Ctrl+C and <c>SIGTERM</c> into the shutdown the tray's Quit item asks for, for as long as it is held.
/// </summary>
/// <remarks>
/// Avalonia handles neither signal, so without this a <c>kill</c>, a <c>systemctl stop</c> or a logout ended a
/// tray run through the runtime's default handler: the tool's stop action never ran, and a toggle clicked inside
/// the preference store's debounce window was never saved. Before the tray exists there is nothing to shut
/// down, so the signal is left to end the process as it would have.
/// </remarks>
internal sealed class ShutdownSignals : IDisposable
{
	private readonly Func<TrayApplication?> host;
	private readonly PosixSignalRegistration termination;

	/// <summary>
	/// Initializes a new instance of the <see cref="ShutdownSignals"/> class, taking both signals.
	/// </summary>
	/// <param name="host">Reads the tray host, which does not exist until Avalonia has called back for it.</param>
	internal ShutdownSignals(Func<TrayApplication?> host)
	{
		Ensure.NotNull(host);
		this.host = host;

		Console.CancelKeyPress += OnCancelKeyPress;

		// SIGTERM does not go through CancelKeyPress.
		termination = PosixSignalRegistration.Create(PosixSignal.SIGTERM, OnTermination);
	}

	/// <summary>
	/// Releases both signals back to their default handling.
	/// </summary>
	public void Dispose()
	{
		Console.CancelKeyPress -= OnCancelKeyPress;
		termination.Dispose();
	}

	/// <summary>
	/// Handles <c>SIGTERM</c>, keeping the process alive only when there is a tray to shut down instead.
	/// </summary>
	/// <param name="context">The signal being delivered.</param>
	internal void OnTermination(PosixSignalContext context) => context.Cancel = RequestShutdown();

	/// <summary>
	/// Asks the tray to shut down, if there is one yet.
	/// </summary>
	/// <returns>
	/// <see langword="true"/> when a tray took the request, so the signal's default handling should be cancelled.
	/// </returns>
	internal bool RequestShutdown()
	{
		if (host() is not TrayApplication running)
		{
			return false;
		}

		_ = running.RequestShutdown();
		return true;
	}

	private void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs eventArgs) => eventArgs.Cancel = RequestShutdown();
}
