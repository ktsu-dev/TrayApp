// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.TrayApp.Test;

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using ktsu.TrayApp;
using ktsu.TrayApp.Menu;
using ktsu.TrayApp.Tray;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests for how a tray run answers Ctrl+C, <c>SIGTERM</c> and <c>SIGHUP</c>.
/// </summary>
/// <remarks>
/// Most of these call the handlers directly, so they show what each one decides without a display or a process
/// to kill. The <c>SIGHUP</c> case sends the real signal, because what it checks is that the signal is taken at
/// all. <c>scripts/check-tray-signals.sh</c> covers the whole run.
/// </remarks>
[TestClass]
[DoNotParallelize]
public class ShutdownSignalsTests
{
	private static TrayApplication Host()
	{
		TrayAppDefinition app = TrayAppBuilder.Create("demo").Toggle("One", () => true, _ => { }).Build();
		return new TrayApplication(app, new TrayMenu(app.DisplayName, app.Status, app.Items), duration: null, onToggled: null);
	}

	[TestMethod]
	public void TerminationBeforeTheTrayExists_IsLeftToEndTheProcess()
	{
		using ShutdownSignals signals = new(() => null);
		PosixSignalContext context = new(PosixSignal.SIGTERM);

		signals.OnTermination(context);

		Assert.IsFalse(context.Cancel);
	}

	[TestMethod]
	public void TerminationOnceTheTrayExists_IsTakenAndShutsTheTrayDown()
	{
		using TrayApplication host = Host();
		using ShutdownSignals signals = new(() => host);
		PosixSignalContext context = new(PosixSignal.SIGTERM);

		signals.OnTermination(context);

		Assert.IsTrue(context.Cancel);

		// The signal already asked, so nothing is left for a second request to post.
		Assert.IsFalse(host.RequestShutdown());
	}

	[TestMethod]
	[OSCondition(OperatingSystems.Linux | OperatingSystems.OSX)]
	public void HangupOnceTheTrayExists_IsTakenAndShutsTheTrayDown()
	{
		using TrayApplication host = Host();
		int asked = 0;
		using ShutdownSignals signals = new(() =>
		{
			Interlocked.Increment(ref asked);
			return host;
		});

		// Keeps the test host alive whatever ShutdownSignals does: without it, a SIGHUP nothing takes ends the
		// process, and the failure would show up as a crashed run instead of this assertion.
		using ManualResetEventSlim delivered = new();
		using PosixSignalRegistration guard = PosixSignalRegistration.Create(PosixSignal.SIGHUP, context =>
		{
			context.Cancel = true;
			delivered.Set();
		});

		using (Process kill = Process.Start("kill", ["-HUP", Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture)]))
		{
			kill.WaitForExit();
			Assert.AreEqual(0, kill.ExitCode);
		}

		Assert.IsTrue(delivered.Wait(TimeSpan.FromSeconds(10)), "SIGHUP was never delivered");
		Assert.IsTrue(SpinWait.SpinUntil(() => Volatile.Read(ref asked) > 0, TimeSpan.FromSeconds(10)), "SIGHUP did not reach the tray");

		// The signal already asked, so nothing is left for a second request to post.
		Assert.IsFalse(host.RequestShutdown());
	}

	[TestMethod]
	public void HangupBeforeTheTrayExists_IsLeftToEndTheProcess()
	{
		using ShutdownSignals signals = new(() => null);
		PosixSignalContext context = new(PosixSignal.SIGHUP);

		signals.OnTermination(context);

		Assert.IsFalse(context.Cancel);
	}

	[TestMethod]
	public void ARepeatedSignal_IsStillTakenButPostsNoSecondShutdown()
	{
		using TrayApplication host = Host();
		using ShutdownSignals signals = new(() => host);

		Assert.IsTrue(signals.RequestShutdown());
		Assert.IsTrue(signals.RequestShutdown());
		Assert.IsFalse(host.RequestShutdown());
	}

	[TestMethod]
	public void RequestShutdown_PostsOnlyOnce()
	{
		using TrayApplication host = Host();

		Assert.IsTrue(host.RequestShutdown());
		Assert.IsFalse(host.RequestShutdown());
	}
}
