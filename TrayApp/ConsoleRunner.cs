// Copyright (c) 2026 ktsu-dev contributors

namespace ktsu.TrayApp;

using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading;
using ktsu.TrayApp.Cli;

/// <summary>
/// The headless mode: start the tool and block until interrupted or until the duration runs out.
/// </summary>
/// <remarks>
/// This is the mode a CI agent, an SSH session, or a script gets, and the mode the tray falls back to when
/// the windowing stack refuses. It touches nothing from the windowing stack, which is what makes
/// <c>--no-tray</c> usable on a machine with no desktop at all.
/// </remarks>
internal static class ConsoleRunner
{
	internal const int ExitSuccess = 0;
	internal const int ExitFailure = 1;

	/// <summary>
	/// Runs the tool until the process is asked to stop.
	/// </summary>
	/// <param name="app">The tool to run.</param>
	/// <param name="options">The options the run was started with.</param>
	/// <returns>The process exit code.</returns>
	[SuppressMessage(
		"Design",
		"CA1031:Do not catch general exception types",
		Justification = "The start action belongs to the consuming tool, so there is no exception type to filter on; reporting the refusal and exiting 1 is the contract the exit codes in --help describe.")]
	internal static int Run(TrayAppDefinition app, CommandLineOptions options)
	{
		Ensure.NotNull(app);
		Ensure.NotNull(options);

		try
		{
			app.OnStart?.Invoke();
		}
		catch (Exception ex)
		{
			Console.Error.WriteLine($"{app.Name}: {ex.Message}");
			return ExitFailure;
		}

		using ManualResetEventSlim stopping = new(initialState: false);

		void RequestStop() => stopping.Set();

		void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs eventArgs)
		{
			// Take the interrupt rather than letting the runtime tear the process down, so whatever the tool
			// is holding is released through the normal path instead of being left to the operating system.
			eventArgs.Cancel = true;
			RequestStop();
		}

		Console.CancelKeyPress += OnCancelKeyPress;

		// SIGTERM is how a service manager or `docker stop` ends this, and it does not go through
		// CancelKeyPress.
		using PosixSignalRegistration termination = PosixSignalRegistration.Create(PosixSignal.SIGTERM, context =>
		{
			context.Cancel = true;
			RequestStop();
		});

		try
		{
			Console.WriteLine(DescribeRun(app, options));

			if (options.Duration is TimeSpan window)
			{
				stopping.Wait(window);
			}
			else
			{
				stopping.Wait();
			}
		}
		finally
		{
			Console.CancelKeyPress -= OnCancelKeyPress;
			app.OnStop?.Invoke();
		}

		Console.WriteLine($"{app.Name}: stopped.");
		return ExitSuccess;
	}

	private static string DescribeRun(TrayAppDefinition app, CommandLineOptions options)
	{
		string until = options.Duration is TimeSpan window
			? string.Create(CultureInfo.InvariantCulture, $", stopping after {window:g}")
			: string.Empty;

		return string.Create(
			CultureInfo.InvariantCulture,
			$"{app.Name}: {app.Describe()}{until}. Press Ctrl+C to stop.");
	}
}
