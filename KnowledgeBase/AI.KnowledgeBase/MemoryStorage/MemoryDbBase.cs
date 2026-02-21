// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AI.KnowledgeBase.FileSystem;
using Microsoft.Extensions.Logging;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.Diagnostics;
using Microsoft.KernelMemory.FileSystem.DevTools;
using Microsoft.KernelMemory.MemoryStorage;

namespace AI.KnowledgeBase.MemoryStorage;

/// <summary>
/// Abstract base class for simple memory databases with common functionality
/// </summary>
public abstract class MemoryDbBase : IMemoryDb
{
    protected readonly IFileSystem _fileSystem;
    protected readonly ILogger _log;

    /// <summary>
    /// Create new instance
    /// </summary>
    /// <param name="config">Simple memory db configuration</param>
    /// <param name="logger">Application logger</param>
    protected MemoryDbBase(
        MemoryDbConfig config,
        ILogger logger)
    {
        if (config == null) throw new ArgumentNullException(nameof(config));
        
        this._fileSystem = CreateFileSystem(config, null);
        this._log = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Create new instance
    /// </summary>
    /// <param name="fileSystem">File system implementation</param>
    /// <param name="logger">Application logger</param>
    protected MemoryDbBase(
        IFileSystem fileSystem,
        ILogger logger)
    {
        this._fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        this._log = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public virtual Task CreateIndexAsync(string index, int vectorSize, CancellationToken cancellationToken = default)
    {
        index = NormalizeIndexName(index);
        return this._fileSystem.CreateVolumeAsync(index, cancellationToken);
    }

    /// <inheritdoc />
    public virtual Task<IEnumerable<string>> GetIndexesAsync(CancellationToken cancellationToken = default)
    {
        return this._fileSystem.ListVolumesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public virtual Task DeleteIndexAsync(string index, CancellationToken cancellationToken = default)
    {
        index = NormalizeIndexName(index);
        return this._fileSystem.DeleteVolumeAsync(index, cancellationToken);
    }

    /// <inheritdoc />
    public virtual async Task<string> UpsertAsync(string index, MemoryRecord record, CancellationToken cancellationToken = default)
    {
        index = NormalizeIndexName(index);
        await this._fileSystem.WriteFileAsync(index, "", EncodeId(record.Id), JsonSerializer.Serialize(record), cancellationToken).ConfigureAwait(false);
        return record.Id;
    }

    /// <inheritdoc />
    public abstract IAsyncEnumerable<(MemoryRecord, double)> GetSimilarListAsync(
        string index,
        string text,
        ICollection<MemoryFilter>? filters = null,
        double minRelevance = 0,
        int limit = 1,
        bool withEmbeddings = false,
        [EnumeratorCancellation] CancellationToken cancellationToken = default);

    /// <inheritdoc />
    public virtual async IAsyncEnumerable<MemoryRecord> GetListAsync(
        string index,
        ICollection<MemoryFilter>? filters = null,
        int limit = 1,
        bool withEmbeddings = false,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (limit <= 0) { limit = int.MaxValue; }

        index = NormalizeIndexName(index);

        // Remove empty filters
        filters = filters?.Where(f => !f.IsEmpty()).ToList();

        IDictionary<string, string> list;
        try
        {
            list = await this._fileSystem.ReadAllFilesAsTextAsync(index, "", cancellationToken).ConfigureAwait(false);
        }
        catch (DirectoryNotFoundException)
        {
            // Index doesn't exist
            list = new Dictionary<string, string>();
        }

        foreach (KeyValuePair<string, string> v in list)
        {
            var record = JsonSerializer.Deserialize<MemoryRecord>(v.Value);
            if (record == null) { continue; }

            if (TagsMatchFilters(record.Tags, filters))
            {
                if (limit-- <= 0) { yield break; }

                yield return record;
            }
        }
    }

    /// <inheritdoc />
    public virtual Task DeleteAsync(string index, MemoryRecord record, CancellationToken cancellationToken = default)
    {
        index = NormalizeIndexName(index);
        return this._fileSystem.DeleteFileAsync(index, "", EncodeId(record.Id), cancellationToken);
    }

    #region Protected Helper Methods

    /// <summary>
    /// Load all records from the index that match the filters
    /// </summary>
    protected async Task<Dictionary<string, MemoryRecord>> LoadFilteredRecordsAsync(
        string index,
        ICollection<MemoryFilter>? filters = null,
        bool withEmbeddings = false,
        CancellationToken cancellationToken = default)
    {
        var records = new Dictionary<string, MemoryRecord>();
        var list = GetListAsync(index, filters, int.MaxValue, withEmbeddings, cancellationToken);
        
        await foreach (MemoryRecord r in list.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            records[r.Id] = r;
        }

        this._log.LogDebug("{RecordCount} records loaded", records.Count);
        return records;
    }

    /// <summary>
    /// Create file system based on configuration
    /// </summary>
    protected static IFileSystem CreateFileSystem(MemoryDbConfig config, ILoggerFactory? loggerFactory = null)
    {
        if (config == null) throw new ArgumentNullException(nameof(config));

        return config.StorageType switch
        {
            EnhancedFileSystemTypes.Disk => new DiskFileSystem(config.Directory, null, loggerFactory),
            EnhancedFileSystemTypes.Volatile => VolatileFileSystem.GetInstance(config.Directory, null, loggerFactory),
            EnhancedFileSystemTypes.Hybrid => new HybridFileSystem(config.Directory, null, loggerFactory),
            _ => throw new ArgumentException($"Unknown storage type {config.StorageType}")
        };
    }

    #endregion

    #region Static Helper Methods

    // Note: normalize "_" to "-" for consistency with other DBs
    private static readonly Regex s_replaceIndexNameCharsRegex = new(@"[\s|\\|/|.|_|:]");
    private const string ValidSeparator = "-";

    protected static string NormalizeIndexName(string index)
    {
        ArgumentNullExceptionEx.ThrowIfNullOrWhiteSpace(index, nameof(index), "The index name is empty");
        index = s_replaceIndexNameCharsRegex.Replace(index.Trim().ToLowerInvariant(), ValidSeparator);

        return index.Trim();
    }

    protected static bool TagsMatchFilters(TagCollection tags, ICollection<MemoryFilter>? filters)
    {
        if (filters == null || filters.Count == 0) { return true; }

        // Verify that at least one filter matches (OR logic)
        foreach (MemoryFilter filter in filters)
        {
            var match = true;

            // Verify that all conditions are met (AND logic)
            foreach (KeyValuePair<string, List<string?>> condition in filter)
            {
                // Check if the tag name + value is present
                for (int index = 0; match && index < condition.Value.Count; index++)
                {
                    match = match && tags.ContainsKey(condition.Key) && tags[condition.Key].Contains(condition.Value[index]);
                }
            }

            if (match) { return true; }
        }

        return false;
    }

    protected static string EncodeId(string realId)
    {
        var bytes = Encoding.UTF8.GetBytes(realId);
        return Convert.ToBase64String(bytes).Replace('=', '_');
    }

    protected static string DecodeId(string encodedId)
    {
        var bytes = Convert.FromBase64String(encodedId.Replace('_', '='));
        return Encoding.UTF8.GetString(bytes);
    }

    #endregion
}
