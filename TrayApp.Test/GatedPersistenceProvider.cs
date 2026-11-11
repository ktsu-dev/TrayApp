// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.TrayApp.Test;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ktsu.Essentials;

/// <summary>
/// Wraps a persistence provider and holds every write until the test lets it through.
/// </summary>
/// <remarks>
/// The debounced store's shutdown races only show when a write is still under way while something else
/// happens, so this makes "under way" a state a test can hold the store in rather than a timing it hopes for.
/// Each <see cref="Open"/> lets one write through, in the order the writes started.
/// </remarks>
internal sealed class GatedPersistenceProvider(IPersistenceProvider<string> inner) : IPersistenceProvider<string>, IDisposable
{
	private readonly SemaphoreSlim gate = new(0);
	private int started;
	private int completed;

	/// <inheritdoc/>
	public string ProviderName => inner.ProviderName;

	/// <inheritdoc/>
	public bool IsPersistent => inner.IsPersistent;

	/// <summary>Gets the number of writes that have reached the gate.</summary>
	public int Started => Volatile.Read(ref started);

	/// <summary>Gets the number of writes that have gone through it and finished.</summary>
	public int Completed => Volatile.Read(ref completed);

	/// <summary>Lets one held write through.</summary>
	public void Open() => gate.Release();

	/// <inheritdoc/>
	public async Task StoreAsync<T>(string key, T obj, CancellationToken cancellationToken = default)
	{
		Interlocked.Increment(ref started);
		await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
		await inner.StoreAsync(key, obj, cancellationToken).ConfigureAwait(false);
		Interlocked.Increment(ref completed);
	}

	/// <inheritdoc/>
	public Task<T?> RetrieveAsync<T>(string key, CancellationToken cancellationToken = default) =>
		inner.RetrieveAsync<T>(key, cancellationToken);

	/// <inheritdoc/>
	public Task<T> RetrieveOrCreateAsync<T>(string key, CancellationToken cancellationToken = default)
		where T : new() =>
		inner.RetrieveOrCreateAsync<T>(key, cancellationToken);

	/// <inheritdoc/>
	public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default) =>
		inner.ExistsAsync(key, cancellationToken);

	/// <inheritdoc/>
	public Task<bool> RemoveAsync(string key, CancellationToken cancellationToken = default) =>
		inner.RemoveAsync(key, cancellationToken);

	/// <inheritdoc/>
	public Task<IEnumerable<string>> GetAllKeysAsync(CancellationToken cancellationToken = default) =>
		inner.GetAllKeysAsync(cancellationToken);

	/// <inheritdoc/>
	public Task ClearAsync(CancellationToken cancellationToken = default) => inner.ClearAsync(cancellationToken);

	/// <inheritdoc/>
	public void Dispose() => gate.Dispose();
}
