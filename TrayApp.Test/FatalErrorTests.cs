// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.TrayApp.Test;

using System;
using System.Diagnostics.CodeAnalysis;
using ktsu.TrayApp;
using ktsu.TrayApp.Menu;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests for the line between a failure the library absorbs and one it must not.
/// </summary>
[TestClass]
public class FatalErrorTests
{
	[TestMethod]
	public void IsNotFatal_ForAnOrdinaryRefusal_IsTrue()
	{
		Assert.IsTrue(FatalError.IsNotFatal(new InvalidOperationException("no inhibitor here")));
		Assert.IsTrue(FatalError.IsNotFatal(new PlatformNotSupportedException()));
		Assert.IsTrue(FatalError.IsNotFatal(new ObjectDisposedException("blocker")));
	}

	[TestMethod]
	[SuppressMessage(
		"Usage",
		"CA2201:Do not raise reserved exception types",
		Justification = "These are the reserved types under test; constructing one is how the filter that excludes them is checked, and none of them is raised.")]
	public void IsNotFatal_ForAProcessThatIsAlreadyLost_IsFalse()
	{
		Assert.IsFalse(FatalError.IsNotFatal(new OutOfMemoryException()));
		Assert.IsFalse(FatalError.IsNotFatal(new AccessViolationException()));
	}

	[TestMethod]
	public void IsNotFatal_ForARefusedAllocation_IsTrue()
	{
		// InsufficientMemoryException derives from OutOfMemoryException but means the opposite: one
		// allocation was refused before being attempted, and the process is fine.
		Assert.IsTrue(FatalError.IsNotFatal(new InsufficientMemoryException()));
	}

	[TestMethod]
	[SuppressMessage(
		"Usage",
		"CA2201:Do not raise reserved exception types",
		Justification = "Throwing the reserved type is the behaviour under test: the menu must let it through rather than report it as a refusal.")]
	public void Run_DoesNotSwallowAFatalException()
	{
		TrayMenu menu = new("Demo", () => "idle", []);

		// The same setter reached from a click and from a preference restore must behave the same way, so
		// the filter sits on both paths rather than only the one a review flagged.
		Assert.ThrowsExactly<OutOfMemoryException>(() => menu.Run(() => throw new OutOfMemoryException()));
		Assert.IsNull(menu.LastError);
	}

	[TestMethod]
	public void Run_StillAbsorbsAnOrdinaryRefusal()
	{
		TrayMenu menu = new("Demo", () => "idle", []);

		Assert.IsFalse(menu.Run(() => throw new InvalidOperationException("nope")));
		Assert.AreEqual("nope", menu.LastError);
	}
}
