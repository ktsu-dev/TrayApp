// Copyright (c) 2026 ktsu-dev contributors

namespace ktsu.TrayApp.Demo;

using System.Threading.Tasks;
using ktsu.Essentials;
using ktsu.Essentials.FileSystemProviders.Native;
using ktsu.Essentials.PersistenceProviders.ConfigHome;
using ktsu.Essentials.SerializationProviders.Json;

/// <summary>
/// A tray tool with one toggle, and nothing else.
/// </summary>
internal static class Program
{
	private static async Task<int> Main(string[] args)
	{
		Stopwatch stopwatch = new();

		// The whole persistence wiring a tool needs: a serializer, a file system, and a directory
		// convention. ktsu.TrayApp sees only the IPersistenceProvider<string> that falls out of it.
		IPersistenceProvider<string> preferences = new ConfigHomePersistenceProvider<string>(
			new NativeFileSystemProvider(),
			new JsonSerializationProvider(),
			"trayapp-demo",
			string.Empty);

		return await TrayAppBuilder.Create("trayapp-demo")
			.DisplayName("TrayApp Demo")
			.Summary("a minimal tray tool built on ktsu.TrayApp.")
			.Icons(typeof(Program).Assembly, "Assets.tray-active.png", "Assets.tray-idle.png")
			.Status(stopwatch.Describe)
			.Toggle("Running", () => stopwatch.IsRunning, stopwatch.SetRunning, persistAs: "running")
			.OnStart(stopwatch.Begin)
			.OnStop(stopwatch.Stop)
			.Preferences(preferences)
			.RunAsync(args)
			.ConfigureAwait(false);
	}
}
