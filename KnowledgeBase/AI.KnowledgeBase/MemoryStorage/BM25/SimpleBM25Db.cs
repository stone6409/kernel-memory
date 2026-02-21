// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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

namespace AI.KnowledgeBase.MemoryStorage.BM25;

/// <summary>
/// BM25-based text similarity implementation for development and testing.
/// Uses BM25 algorithm for text similarity search without requiring embedding generators.
/// </summary>
[Experimental("KMEXP03")]
public class SimpleBM25Db : IMemoryDb
{
    private readonly IFileSystem _fileSystem;
    private readonly ILogger<SimpleBM25Db> _log;
    private readonly BM25Algorithm _bm25Algorithm;

    /// <summary>
    /// Create new instance
    /// </summary>
    /// <param name="config">Simple BM25 db settings</param>
    /// <param name="loggerFactory">Application logger factory</param>
    public SimpleBM25Db(
        SimpleBM25DbConfig config,
        ILoggerFactory? loggerFactory = null)
        : this(config, new BM25Parameters(), loggerFactory)
    {
    }

    /// <summary>
    /// Create new instance with custom BM25 parameters
    /// </summary>
    /// <param name="config">Simple BM25 db settings</param>
    /// <param name="parameters">BM25 algorithm parameters</param>
    /// <param name="loggerFactory">Application logger factory</param>
    public SimpleBM25Db(
        SimpleBM25DbConfig config,
        BM25Parameters parameters,
        ILoggerFactory? loggerFactory = null)
    {
        this._log = (loggerFactory ?? DefaultLogger.Factory).CreateLogger<SimpleBM25Db>();
        this._bm25Algorithm = new BM25Algorithm(parameters);

        switch (config.StorageType)
        {
            case FileSystemTypes.Disk:
                this._fileSystem = new DiskFileSystem(config.Directory, null, loggerFactory);
                break;

            case FileSystemTypes.Volatile:
                this._fileSystem = VolatileFileSystem.GetInstance(config.Directory, null, loggerFactory);
                break;

            default:
                throw new ArgumentException($"Unknown storage type {config.StorageType}");
        }
    }

    /// <inheritdoc />
    public Task CreateIndexAsync(string index, int vectorSize, CancellationToken cancellationToken = default)
    {
        index = NormalizeIndexName(index);
        return this._fileSystem.CreateVolumeAsync(index, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IEnumerable<string>> GetIndexesAsync(CancellationToken cancellationToken = default)
    {
        return this._fileSystem.ListVolumesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task DeleteIndexAsync(string index, CancellationToken cancellationToken = default)
    {
        index = NormalizeIndexName(index);
        return this._fileSystem.DeleteVolumeAsync(index, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<string> UpsertAsync(string index, MemoryRecord record, CancellationToken cancellationToken = default)
    {
        index = NormalizeIndexName(index);
        await this._fileSystem.WriteFileAsync(index, "", EncodeId(record.Id), JsonSerializer.Serialize(record), cancellationToken).ConfigureAwait(false);
        return record.Id;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<(MemoryRecord, double)> GetSimilarListAsync(
        string index,
        string text,
        ICollection<MemoryFilter>? filters = null,
        double minRelevance = 0,
        int limit = 1,
        bool withEmbeddings = false,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (limit <= 0) { limit = int.MaxValue; }

        index = NormalizeIndexName(index);

        // Get all records matching filters
        var list = this.GetListAsync(index, filters, int.MaxValue, withEmbeddings, cancellationToken);
        var records = new Dictionary<string, MemoryRecord>();
        await foreach (MemoryRecord r in list.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            records[r.Id] = r;
        }

        this._log.LogDebug("{RecordCount} records loaded for BM25 similarity check", records.Count);

        if (records.Count == 0)
        {
            yield break;
        }

        // Prepare documents for BM25 calculation
        var documents = PrepareDocuments(records);
        if (documents.Count == 0)
        {
            yield break;
        }

        // Calculate BM25 scores
        var scores = _bm25Algorithm.CalculateScores(documents, text);
        scores = BM25Normalizer.NormalizeScores(scores);

        // Sort by score descending and filter by minRelevance
        var sortedResults = scores
            .Where(kvp => kvp.Value >= minRelevance)
            .OrderByDescending(kvp => kvp.Value)
            .Take(limit)
            .ToList();

        // Return results
        foreach (var result in sortedResults)
        {
            yield return (records[result.Key], result.Value);
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<MemoryRecord> GetListAsync(
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
    public Task DeleteAsync(string index, MemoryRecord record, CancellationToken cancellationToken = default)
    {
        index = NormalizeIndexName(index);
        return this._fileSystem.DeleteFileAsync(index, "", EncodeId(record.Id), cancellationToken);
    }

    #region Helper Methods

    /// <summary>
    /// Prepare documents from memory records for BM25 calculation
    /// </summary>
    private List<BM25Document> PrepareDocuments(Dictionary<string, MemoryRecord> records)
    {
        var documents = new List<BM25Document>();

        foreach (var record in records)
        {
            var storedText = record.Value.Payload[Constants.ReservedPayloadTextField]?.ToString();
            if (string.IsNullOrEmpty(storedText))
            {
                continue;
            }

            // Use BM25Algorithm to create document with tokenized text
            var document = _bm25Algorithm.CreateDocument(record.Key, storedText, record.Value);
            if (document.Tokens.Count == 0)
            {
                continue;
            }

            documents.Add(document);
        }

        return documents;
    }

    /// <summary>
    /// Get the BM25 algorithm instance for advanced usage
    /// </summary>
    public BM25Algorithm GetBM25Algorithm() => _bm25Algorithm;

    #endregion

    #region private

    // Note: normalize "_" to "-" for consistency with other DBs
    private static readonly Regex s_replaceIndexNameCharsRegex = new(@"[\s|\\|/|.|_|:]");
    private const string ValidSeparator = "-";

    private static string NormalizeIndexName(string index)
    {
        ArgumentNullExceptionEx.ThrowIfNullOrWhiteSpace(index, nameof(index), "The index name is empty");
        index = s_replaceIndexNameCharsRegex.Replace(index.Trim().ToLowerInvariant(), ValidSeparator);

        return index.Trim();
    }

    private static bool TagsMatchFilters(TagCollection tags, ICollection<MemoryFilter>? filters)
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

    private static string EncodeId(string realId)
    {
        var bytes = Encoding.UTF8.GetBytes(realId);
        return Convert.ToBase64String(bytes).Replace('=', '_');
    }

    private static string DecodeId(string encodedId)
    {
        var bytes = Convert.FromBase64String(encodedId.Replace('_', '='));
        return Encoding.UTF8.GetString(bytes);
    }

    #endregion
}
