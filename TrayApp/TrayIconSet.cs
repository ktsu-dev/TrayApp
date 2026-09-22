// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.TrayApp;

using System;
using System.IO;
using System.Reflection;
using Avalonia.Controls;

/// <summary>
/// The tool's tray images, loaded once from its assembly's embedded resources.
/// </summary>
/// <remarks>
/// <para>
/// Images are embedded rather than shipped beside the assembly because a dotnet tool is installed as a
/// package payload, and a loose PNG next to the dll is one more thing that can go missing on a machine the
/// tool will never be debugged on.
/// </para>
/// <para>
/// Loading is deferred: a <see cref="WindowIcon"/> reaches for the platform's imaging support, which does not
/// exist until Avalonia has started.
/// </para>
/// </remarks>
public sealed class TrayIconSet
{
	private readonly Lazy<WindowIcon> active;
	private readonly Lazy<WindowIcon> idle;

	private TrayIconSet(Assembly assembly, string activeResourceName, string idleResourceName)
	{
		Assembly = assembly;
		ActiveResourceName = activeResourceName;
		IdleResourceName = idleResourceName;

		active = new Lazy<WindowIcon>(() => Load(assembly, activeResourceName));
		idle = new Lazy<WindowIcon>(() => Load(assembly, idleResourceName));
	}

	/// <summary>
	/// Gets the assembly the images are embedded in.
	/// </summary>
	public Assembly Assembly { get; }

	/// <summary>
	/// Gets the resource name of the image shown while the tool is doing its job.
	/// </summary>
	public string ActiveResourceName { get; }

	/// <summary>
	/// Gets the resource name of the image shown while the tool is idle.
	/// </summary>
	public string IdleResourceName { get; }

	/// <summary>
	/// Gets the image shown while the tool is doing its job.
	/// </summary>
	public WindowIcon Active => active.Value;

	/// <summary>
	/// Gets the image shown while the tool is idle.
	/// </summary>
	public WindowIcon Idle => idle.Value;

	/// <summary>
	/// Loads a pair of images for the active and idle states.
	/// </summary>
	/// <param name="assembly">The assembly the images are embedded in.</param>
	/// <param name="activeResourceName">
	/// The active image's resource name, either in full or relative to the assembly name -
	/// <c>Assets.tray-active.png</c> resolves against <c>ktsu.Example.Assets.tray-active.png</c>.
	/// </param>
	/// <param name="idleResourceName">The idle image's resource name, resolved the same way.</param>
	/// <returns>The icon set.</returns>
	/// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
	public static TrayIconSet FromResources(Assembly assembly, string activeResourceName, string idleResourceName)
	{
		Ensure.NotNull(assembly);
		Ensure.NotNull(activeResourceName);
		Ensure.NotNull(idleResourceName);

		return new TrayIconSet(assembly, activeResourceName, idleResourceName);
	}

	/// <summary>
	/// Loads one image, used for both states.
	/// </summary>
	/// <param name="assembly">The assembly the image is embedded in.</param>
	/// <param name="resourceName">The image's resource name, resolved as in <see cref="FromResources"/>.</param>
	/// <returns>The icon set.</returns>
	/// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
	public static TrayIconSet FromResource(Assembly assembly, string resourceName) =>
		FromResources(assembly, resourceName, resourceName);

	/// <summary>
	/// Gets the image for a state.
	/// </summary>
	/// <param name="isActive">Whether the tool is currently doing its job.</param>
	/// <returns>The matching image.</returns>
	public WindowIcon For(bool isActive) => isActive ? Active : Idle;

	/// <summary>
	/// Opens an embedded resource by name.
	/// </summary>
	/// <param name="assembly">The assembly to look in.</param>
	/// <param name="resourceName">The resource name, in full or relative to the assembly name.</param>
	/// <returns>The open stream; the caller disposes it.</returns>
	/// <exception cref="InvalidOperationException">No resource of that name is embedded in the assembly.</exception>
	/// <remarks>
	/// The failure message lists every resource the assembly does have. Resource naming is the thing that
	/// silently breaks - a renamed folder, a file marked Content instead of EmbeddedResource, a project whose
	/// root namespace is not its assembly name - and that list turns a runtime mystery into a one-line fix.
	/// </remarks>
	internal static Stream OpenResource(Assembly assembly, string resourceName)
	{
		Stream? stream = assembly.GetManifestResourceStream(resourceName)
			?? assembly.GetManifestResourceStream($"{assembly.GetName().Name}.{resourceName}");

		return stream ?? throw new InvalidOperationException(
			$"The tray icon '{resourceName}' is missing from {assembly.GetName().Name}. Available resources: {string.Join(", ", assembly.GetManifestResourceNames())}.");
	}

	private static WindowIcon Load(Assembly assembly, string resourceName)
	{
		using Stream stream = OpenResource(assembly, resourceName);
		return new WindowIcon(stream);
	}
}
