// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.TrayApp.Test;

using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using ktsu.Essentials;
using ktsu.Essentials.PersistenceProviders.InMemory;
using ktsu.TrayApp.Preferences;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests for <see cref="DebouncedPreferenceStore"/> against a real persistence provider.
/// </summary>
/// <remarks>
/// The provider under these tests is <c>ktsu.Essentials.PersistenceProviders.InMemory</c>, so the round trip
/// is the real one the library performs - only without a directory to clean up afterwards.
/// </remarks>
[TestClass]
public class DebouncedPreferenceStoreTests
{
	private static readonly TimeSpan Debounce = TimeSpan.FromMilliseconds(120);
	private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);
	private const string Key = "tray";

	private static DebouncedPreferenceStore CreateStore(IPersistenceProvider<string> provider) =>
		new(provider, Key, Debounce);

	[TestMethod]
	public async Task LoadAsync_WithNothingStored_ReportsNothingRemembered()
	{
		InMemoryPersistenceProvider<string> provider = new();
		using DebouncedPreferenceStore store = CreateStore(provider);

		await store.LoadAsync().ConfigureAwait(false);

		Assert.IsNull(store.Get("running"));
	}

	[TestMethod]
	public async Task FlushAsync_ThenLoad_RoundTripsThroughTheProvider()
	{
		InMemoryPersistenceProvider<string> provider = new();

		using (DebouncedPreferenceStore writer = CreateStore(provider))
		{
			await writer.LoadAsync().ConfigureAwait(false);
			writer.Set("running", true);
			writer.Set("verbose", false);
			await writer.FlushAsync().ConfigureAwait(false);
		}

		using DebouncedPreferenceStore reader = CreateStore(provider);
		await reader.LoadAsync().ConfigureAwait(false);

		Assert.IsTrue(reader.Get("running"));
		Assert.IsFalse(reader.Get("verbose"));
		Assert.IsNull(reader.Get("missing"));
	}

	[TestMethod]
	public async Task Set_CalledRepeatedly_WritesOnceAfterTheBurst()
	{
		CountingPersistenceProvider provider = new(new InMemoryPersistenceProvider<string>());
		using DebouncedPreferenceStore store = CreateStore(provider);
		await store.LoadAsync().ConfigureAwait(false);

		// A user flicking a toggle back and forth; without the debounce this is one file write per flick.
		for (int i = 0; i < 8; i++)
		{
			store.Set("running", i % 2 == 0);
		}

		await WaitForWritesAsync(provider, 1).ConfigureAwait(false);

		// The timer restarts on every change, so the burst produced exactly one write and no trailing one.
		await Task.Delay(Debounce * 4).ConfigureAwait(false);
		Assert.AreEqual(1, provider.Writes);

		using DebouncedPreferenceStore reader = CreateStore(provider);
		await reader.LoadAsync().ConfigureAwait(false);
		Assert.IsFalse(reader.Get("running"));
	}

	[TestMethod]
	public async Task FlushAsync_WithNothingChanged_WritesNothing()
	{
		CountingPersistenceProvider provider = new(new InMemoryPersistenceProvider<string>());
		using DebouncedPreferenceStore store = CreateStore(provider);
		await store.LoadAsync().ConfigureAwait(false);

		await store.FlushAsync().ConfigureAwait(false);

		Assert.AreEqual(0, provider.Writes);
	}

	[TestMethod]
	public async Task FlushAsync_AfterAChange_WritesImmediatelyAndCancelsTheTimer()
	{
		CountingPersistenceProvider provider = new(new InMemoryPersistenceProvider<string>());
		using DebouncedPreferenceStore store = CreateStore(provider);
		await store.LoadAsync().ConfigureAwait(false);

		store.Set("running", true);
		await store.FlushAsync().ConfigureAwait(false);

		Assert.AreEqual(1, provider.Writes);

		// This is the exit path: the flush has to beat the debounce, and the cancelled timer must not add a
		// second write behind it.
		await Task.Delay(Debounce * 4).ConfigureAwait(false);
		Assert.AreEqual(1, provider.Writes);
	}

	[TestMethod]
	public async Task Set_WithTheValueItAlreadyHas_DoesNotScheduleAWrite()
	{
		CountingPersistenceProvider provider = new(new InMemoryPersistenceProvider<string>());

		using (DebouncedPreferenceStore seed = CreateStore(provider))
		{
			await seed.LoadAsync().ConfigureAwait(false);
			seed.Set("running", true);
			await seed.FlushAsync().ConfigureAwait(false);
		}

		using DebouncedPreferenceStore store = CreateStore(provider);
		await store.LoadAsync().ConfigureAwait(false);

		store.Set("running", true);
		await store.FlushAsync().ConfigureAwait(false);

		Assert.AreEqual(1, provider.Writes);
	}

	[TestMethod]
	public async Task Get_AfterSet_ReportsTheNewValueBeforeItIsWritten()
	{
		CountingPersistenceProvider provider = new(new InMemoryPersistenceProvider<string>());
		using DebouncedPreferenceStore store = CreateStore(provider);
		await store.LoadAsync().ConfigureAwait(false);

		store.Set("running", true);

		// The menu reads what it just set, not what is on disk; that is what makes a synchronous click
		// possible over an asynchronous provider.
		Assert.IsTrue(store.Get("running"));
		Assert.AreEqual(0, provider.Writes);

		await store.FlushAsync().ConfigureAwait(false);
	}

	[TestMethod]
	public async Task Dispose_WhileADebouncedWriteIsUnderWay_ReportsNoFailure()
	{
		using GatedPersistenceProvider provider = new(new InMemoryPersistenceProvider<string>());
		using StringWriter errors = new();
		DebouncedPreferenceStore store = new(provider, Key, Debounce, errors);
		await store.LoadAsync().ConfigureAwait(false);

		// The exit path: the flush holds the write while a change made just before it lets the debounce fire,
		// so the timer's callback is waiting for the write when the flush finishes and the store is disposed.
		store.Set("running", true);
		Task flush = store.FlushAsync();
		await WaitUntilAsync(() => provider.Started == 1).ConfigureAwait(false);

		store.Set("verbose", true);
		await Task.Delay(Debounce * 3).ConfigureAwait(false);

		provider.Open();
		await flush.ConfigureAwait(false);
		store.Dispose();

		// The callback's own write goes through after the disposal; it must not report the save as failed.
		await WaitUntilAsync(() => provider.Started == 2).ConfigureAwait(false);
		provider.Open();
		await WaitUntilAsync(() => provider.Completed == 2).ConfigureAwait(false);
		await Task.Delay(Debounce * 2).ConfigureAwait(false);

		Assert.AreEqual(string.Empty, errors.ToString());
	}

	private static async Task WaitUntilAsync(Func<bool> condition)
	{
		Stopwatch elapsed = Stopwatch.StartNew();

		while (!condition() && elapsed.Elapsed < Patience)
		{
			await Task.Delay(20).ConfigureAwait(false);
		}

		Assert.IsTrue(condition(), $"The condition did not hold within {Patience}.");
	}

	private static async Task WaitForWritesAsync(CountingPersistenceProvider provider, int expected)
	{
		Stopwatch elapsed = Stopwatch.StartNew();

		while (provider.Writes < expected && elapsed.Elapsed < Patience)
		{
			await Task.Delay(20).ConfigureAwait(false);
		}

		Assert.AreEqual(expected, provider.Writes, $"Expected {expected} write(s) within {Patience}.");
	}
}
