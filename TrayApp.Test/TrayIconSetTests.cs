// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.TrayApp.Test;

using System;
using System.IO;
using System.Reflection;
using ktsu.TrayApp;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests for the resource lookup behind <see cref="TrayIconSet"/>.
/// </summary>
/// <remarks>
/// Resource naming is the thing that silently breaks - a renamed folder, a file marked Content instead of
/// EmbeddedResource, a project whose root namespace is not its assembly name - and the only symptom is a
/// tray icon that never appears. The failure message listing what the assembly does have is what turns that
/// into a one-line fix, so it is worth a test of its own.
/// </remarks>
[TestClass]
public class TrayIconSetTests
{
	private const string SampleResource = "Assets.sample.png";

	private static Assembly ThisAssembly => typeof(TrayIconSetTests).Assembly;

	[TestMethod]
	public void OpenResource_WithAFullName_FindsIt()
	{
		using Stream stream = TrayIconSet.OpenResource(ThisAssembly, $"{ThisAssembly.GetName().Name}.{SampleResource}");
		Assert.IsGreaterThan(0, stream.Length);
	}

	[TestMethod]
	public void OpenResource_WithANameRelativeToTheAssembly_FindsIt()
	{
		// The form the builder's example uses: "Assets.tray-active.png", not the fully qualified name.
		using Stream stream = TrayIconSet.OpenResource(ThisAssembly, SampleResource);
		Assert.IsGreaterThan(0, stream.Length);
	}

	[TestMethod]
	public void OpenResource_WithAMissingName_ListsWhatTheAssemblyHas()
	{
		InvalidOperationException ex = Assert.ThrowsExactly<InvalidOperationException>(
			() => TrayIconSet.OpenResource(ThisAssembly, "Assets.nothing-like-this.png"));

		Assert.Contains("Assets.nothing-like-this.png", ex.Message, StringComparison.Ordinal);
		Assert.Contains("Available resources:", ex.Message, StringComparison.Ordinal);
		Assert.Contains(SampleResource, ex.Message, StringComparison.Ordinal);
	}

	[TestMethod]
	public void FromResources_KeepsTheNamesWithoutLoadingAnything()
	{
		// Loading is deferred because a WindowIcon reaches for Avalonia's imaging support, which does not
		// exist until the application has started; constructing the set must not.
		TrayIconSet icons = TrayIconSet.FromResources(ThisAssembly, "a.png", "b.png");

		Assert.AreEqual("a.png", icons.ActiveResourceName);
		Assert.AreEqual("b.png", icons.IdleResourceName);
		Assert.AreSame(ThisAssembly, icons.Assembly);
	}

	[TestMethod]
	public void FromResource_UsesTheSameImageForBothStates()
	{
		TrayIconSet icons = TrayIconSet.FromResource(ThisAssembly, SampleResource);

		Assert.AreEqual(SampleResource, icons.ActiveResourceName);
		Assert.AreEqual(SampleResource, icons.IdleResourceName);
	}
}
