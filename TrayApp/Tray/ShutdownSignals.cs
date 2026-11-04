// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.TrayApp.Tray;

using System;
using System.Runtime.InteropServices;

/// <summary>
/// Turns Ctrl+C, <c>SIGTERM</c> and <c>SIGHUP</c> into the shutdown the tray's Quit item asks for, for as long as
/// it is held.
/// </summary>
/// <remarks>
/// Avalonia handles none of these signals, so without this a <c>kill</c>, a <c>systemctl stop</c>, a logout or
/// closing the terminal the tool was started from ended a tray run through the runtime's default handler: the
/// tool's stop action never ran, and a toggle clicked inside the preference store's debounce window was never
/// saved. Before the tray exists there is nothing to shut
/// down, so the signal is left to end the process as it would have.
/// </remarks>
internal sealed class ShutdownSignals : IDisposable
{
	private readonly Func<TrayApplication?> host;
	private readonly PosixSignalRegistration termination;
	private readonly PosixSignalRegistration hangup;

	/// <summary>
	/// Initializes a new instance of the <see cref="ShutdownSignals"/> class, taking all three signals.
	/// </summary>
	/// <param name="host">Reads the tray host, which does not exist until Avalonia has called back for it.</param>
	internal ShutdownSignals(Func<TrayApplication?> host)
	{
		Ensure.NotNull(host);
		this.host = host;

		Console.CancelKeyPress += OnCancelKeyPress;

		// SIGTERM and SIGHUP do not go through CancelKeyPress.
		termination = PosixSignalRegistration.Create(PosixSignal.SIGTERM, OnTermination);
		hangup = PosixSignalRegistration.Create(PosixSignal.SIGHUP, OnTermination);
	}

	/// <summary>
	/// Releases all three signals back to their default handling.
	/// </summary>
	public void Dispose()
	{
		Console.CancelKeyPress -= OnCancelKeyPress;
		termination.Dispose();
		hangup.Dispose();
	}

	/// <summary>
	/// Handles <c>SIGTERM</c> and <c>SIGHUP</c>, keeping the process alive only when there is a tray to shut down instead.
	/// </summary>
	/// <param name="context">The signal being delivered.</param>
	internal void OnTermination(PosixSignalContext context) => context.Cancel = RequestShutdown();

	/// <summary>
	/// Asks the tray to shut down, if there is one yet.
	/// </summary>
	/// <remarks>
	/// Only the signal that posts the shutdown is taken. A shutdown that cannot finish - the UI thread blocked in
	/// the tool's code, or a hung stop action - would otherwise leave every later Ctrl+C, <c>kill</c> or closed
	/// terminal cancelled too, and nothing short of <c>SIGKILL</c> would end the process. So, as is usual for a
	/// command-line tool, a repeated signal gets its default handling and ends the process.
	/// </remarks>
	/// <returns>
	/// <see langword="true"/> when this request posted the tray's shutdown, so the signal's default handling should
	/// be cancelled; <see langword="false"/> when there is no tray yet or a shutdown was already posted.
	/// </returns>
	internal bool RequestShutdown() => host() is TrayApplication running && running.RequestShutdown();

	private void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs eventArgs) => eventArgs.Cancel = RequestShutdown();
}
