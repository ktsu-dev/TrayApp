// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.TrayApp.Test;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ktsu.Essentials;

/// <summary>
/// Wraps a persistence provider and counts the writes that reach it.
/// </summary>
/// <remarks>
/// Coalescing is the whole point of the debounced store, and "wrote the right value in the end" does not
/// distinguish a store that writes once from one that writes on every click. The inner provider is the real
/// <c>InMemoryPersistenceProvider</c>, so the round trip being counted is a real one.
/// </remarks>
internal sealed class CountingPersistenceProvider(IPersistenceProvider<string> inner) : IPersistenceProvider<string>
{
	private int writes;

	/// <inheritdoc/>
	public string ProviderName => inner.ProviderName;

	/// <inheritdoc/>
	public bool IsPersistent => inner.IsPersistent;

	/// <summary>Gets the number of times a value has been stored.</summary>
	public int Writes => Volatile.Read(ref writes);

	/// <inheritdoc/>
	public Task StoreAsync<T>(string key, T obj, CancellationToken cancellationToken = default)
	{
		Interlocked.Increment(ref writes);
		return inner.StoreAsync(key, obj, cancellationToken);
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
}
