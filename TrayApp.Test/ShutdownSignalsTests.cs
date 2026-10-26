// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.TrayApp.Test;

using System.Runtime.InteropServices;
using ktsu.TrayApp;
using ktsu.TrayApp.Menu;
using ktsu.TrayApp.Tray;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests for how a tray run answers Ctrl+C and <c>SIGTERM</c>.
/// </summary>
/// <remarks>
/// The signals themselves are not sent: the handlers are called directly, so the tests show what each one
/// decides without a display or a process to kill. <c>scripts/check-tray-signals.sh</c> covers the real thing.
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
