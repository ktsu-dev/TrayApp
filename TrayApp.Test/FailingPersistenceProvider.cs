// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.TrayApp.Test;

using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ktsu.Essentials;
using ktsu.Essentials.PersistenceProviders.InMemory;

/// <summary>
/// A provider that refuses to read, to write, or both: a corrupt file, a read-only home directory, a full disk.
/// </summary>
internal sealed class FailingPersistenceProvider : IPersistenceProvider<string>
{
	private readonly IPersistenceProvider<string> inner = new InMemoryPersistenceProvider<string>();

	/// <summary>Gets or sets a value indicating whether a read throws.</summary>
	public bool FailReads { get; set; }

	/// <summary>Gets or sets a value indicating whether a write throws.</summary>
	public bool FailWrites { get; set; }

	/// <inheritdoc/>
	public string ProviderName => "Failing";

	/// <inheritdoc/>
	public bool IsPersistent => true;

	/// <inheritdoc/>
	public Task StoreAsync<T>(string key, T obj, CancellationToken cancellationToken = default) =>
		FailWrites ? throw new IOException("the disk is full") : inner.StoreAsync(key, obj, cancellationToken);

	/// <inheritdoc/>
	public Task<T?> RetrieveAsync<T>(string key, CancellationToken cancellationToken = default) =>
		FailReads ? throw new InvalidDataException("tray.json is not valid JSON") : inner.RetrieveAsync<T>(key, cancellationToken);

	/// <inheritdoc/>
	public Task<T> RetrieveOrCreateAsync<T>(string key, CancellationToken cancellationToken = default)
		where T : new() =>
		FailReads ? throw new InvalidDataException("tray.json is not valid JSON") : inner.RetrieveOrCreateAsync<T>(key, cancellationToken);

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
