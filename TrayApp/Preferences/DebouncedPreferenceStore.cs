// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.TrayApp.Preferences;

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using ktsu.Essentials;

/// <summary>
/// Holds the tray's remembered state in memory and writes it back in coalesced bursts.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="IPersistenceProvider{TKey}"/> is asynchronous and a menu click is not: there is nothing useful
/// to await in <c>Click</c>, and a user flicking a toggle back and forth would otherwise queue one file write
/// per flick. So the current value lives here, a change restarts a short timer, and the write happens once
/// the flicking stops - or at the latest when <see cref="FlushAsync"/> runs on the way out.
/// </para>
/// <para>
/// Nothing here is created unless the tool called <c>Preferences</c>. A tool that persists nothing does no
/// I/O and needs no provider.
/// </para>
/// </remarks>
internal sealed class DebouncedPreferenceStore : IDisposable
{
	private readonly IPersistenceProvider<string> provider;
	private readonly string key;
	private readonly TimeSpan debounce;
	private readonly SemaphoreSlim writing = new(1, 1);
	private readonly Lock gate = new();
	private readonly Timer timer;

	private Dictionary<string, bool> values = [];
	private bool dirty;
	private bool disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="DebouncedPreferenceStore"/> class.
	/// </summary>
	/// <param name="provider">Where the state is stored.</param>
	/// <param name="key">The key it is stored under.</param>
	/// <param name="debounce">How long a burst of changes is coalesced for.</param>
	internal DebouncedPreferenceStore(IPersistenceProvider<string> provider, string key, TimeSpan debounce)
	{
		Ensure.NotNull(provider);
		Ensure.NotNull(key);

		this.provider = provider;
		this.key = key;
		this.debounce = debounce;
		timer = new Timer(OnDebounceElapsed, state: null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
	}

	/// <summary>
	/// Reads the remembered state, replacing anything held in memory.
	/// </summary>
	/// <param name="cancellationToken">Cancels the read.</param>
	/// <returns>A task that completes when the state has been read.</returns>
	internal async Task LoadAsync(CancellationToken cancellationToken = default)
	{
		TrayPreferences loaded = await provider.RetrieveOrCreateAsync<TrayPreferences>(key, cancellationToken).ConfigureAwait(false);

		lock (gate)
		{
			values = new Dictionary<string, bool>(loaded.Toggles, StringComparer.Ordinal);
			dirty = false;
		}
	}

	/// <summary>
	/// Gets a remembered value.
	/// </summary>
	/// <param name="name">The toggle's preference key.</param>
	/// <returns>The remembered value, or <see langword="null"/> when nothing was remembered for it.</returns>
	internal bool? Get(string name)
	{
		lock (gate)
		{
			return values.TryGetValue(name, out bool value) ? value : null;
		}
	}

	/// <summary>
	/// Records a value and schedules a write.
	/// </summary>
	/// <param name="name">The toggle's preference key.</param>
	/// <param name="value">The value to remember.</param>
	internal void Set(string name, bool value)
	{
		lock (gate)
		{
			if (values.TryGetValue(name, out bool existing) && existing == value)
			{
				return;
			}

			values[name] = value;
			dirty = true;
		}

		// Restarting the timer rather than letting the first change's deadline stand is what makes this a
		// debounce rather than a throttle: a run of clicks writes once, after the last of them.
		timer.Change(debounce, Timeout.InfiniteTimeSpan);
	}

	/// <summary>
	/// Cancels any pending write and writes now, if there is anything to write.
	/// </summary>
	/// <param name="cancellationToken">Cancels the write.</param>
	/// <returns>A task that completes when the state is on disk.</returns>
	internal async Task FlushAsync(CancellationToken cancellationToken = default)
	{
		timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
		await WriteIfDirtyAsync(cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		if (disposed)
		{
			return;
		}

		disposed = true;
		timer.Dispose();
		writing.Dispose();
	}

	[SuppressMessage(
		"Design",
		"CA1031:Do not catch general exception types",
		Justification = "This runs on a timer thread with nothing to return an exception to, and a provider that cannot write - a read-only home directory, a full disk - must not take down a tool the user started for another reason.")]
	private async void OnDebounceElapsed(object? state)
	{
		try
		{
			await WriteIfDirtyAsync(CancellationToken.None).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			await Console.Error.WriteLineAsync($"Could not save preferences: {ex.Message}").ConfigureAwait(false);
		}
	}

	private async Task WriteIfDirtyAsync(CancellationToken cancellationToken)
	{
		await writing.WaitAsync(cancellationToken).ConfigureAwait(false);

		try
		{
			TrayPreferences snapshot;

			lock (gate)
			{
				if (!dirty)
				{
					return;
				}

				dirty = false;
				snapshot = new TrayPreferences { Toggles = new Dictionary<string, bool>(values, StringComparer.Ordinal) };
			}

			try
			{
				await provider.StoreAsync(key, snapshot, cancellationToken).ConfigureAwait(false);
			}
			catch
			{
				// The value is still only in memory, so the next change - or the flush on the way out - has to
				// try again rather than believe this one landed.
				lock (gate)
				{
					dirty = true;
				}

				throw;
			}
		}
		finally
		{
			writing.Release();
		}
	}
}
