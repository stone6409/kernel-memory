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

namespace AI.KnowledgeBase.MemoryStorage;

/// <summary>
/// BM25-based text similarity implementation for development and testing.
/// Uses BM25 algorithm for text similarity search without requiring embedding generators.
/// </summary>
[Experimental("KMEXP03")]
public class SimpleBM25Db : IMemoryDb
{
    private readonly IFileSystem _fileSystem;
    private readonly ILogger<SimpleBM25Db> _log;
    private readonly BM25Parameters _parameters;

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
        this._parameters = parameters ?? new BM25Parameters();

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

        // Tokenize query text
        var queryTokens = TokenizeText(text);
        if (queryTokens.Count == 0)
        {
            yield break;
        }

        // Prepare document collection for BM25 calculation
        var documents = new List<BM25Document>();
        foreach (var record in records)
        {
            var storedText = record.Value.Payload[Constants.ReservedPayloadTextField]?.ToString();
            if (string.IsNullOrEmpty(storedText))
            {
                continue;
            }

            var docTokens = TokenizeText(storedText);
            if (docTokens.Count == 0)
            {
                continue;
            }

            documents.Add(new BM25Document
            {
                Id = record.Key,
                Text = storedText,
                Tokens = docTokens,
                Record = record.Value
            });
        }

        if (documents.Count == 0)
        {
            yield break;
        }

        // Calculate BM25 scores
        var scores = CalculateBM25Scores(documents, queryTokens);

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

    #region BM25 Implementation

    private class BM25Document
    {
        public string Id { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public List<string> Tokens { get; set; } = new List<string>();
        public MemoryRecord Record { get; set; } = null!;
    }

    /// <summary>
    /// Tokenize text into terms
    /// </summary>
    private List<string> TokenizeText(string text)
    {
        // Use Unicode-aware tokenization to support multiple languages
        var tokens = Regex.Replace(text, @"[^\p{L}0-9_]+", " ")
            .Split(' ')
            .Select(x => x.Trim().ToLowerInvariant())
            .Where(x => !string.IsNullOrEmpty(x) && x.Length > 1) // Filter out single characters
            .ToList();

        return tokens;
    }

    /// <summary>
    /// Calculate BM25 scores for documents against query
    /// </summary>
    private Dictionary<string, double> CalculateBM25Scores(List<BM25Document> documents, List<string> queryTokens)
    {
        var scores = new Dictionary<string, double>();
        var N = documents.Count; // Total number of documents
        var avgDocLength = documents.Average(d => d.Tokens.Count);

        // Calculate document frequency for each query term
        var docFrequencies = new Dictionary<string, int>();
        foreach (var term in queryTokens.Distinct())
        {
            docFrequencies[term] = documents.Count(d => d.Tokens.Contains(term));
        }

        // Calculate BM25 score for each document
        foreach (var doc in documents)
        {
            var docLength = doc.Tokens.Count;
            var score = 0.0;

            foreach (var term in queryTokens.Distinct())
            {
                var termFrequency = doc.Tokens.Count(t => t == term);
                if (termFrequency < _parameters.MinTermFrequency)
                {
                    continue;
                }

                var df = docFrequencies[term];
                if (df == 0)
                {
                    continue;
                }

                // Calculate IDF (Inverse Document Frequency)
                var idf = CalculateIDF(N, df);

                // Calculate TF (Term Frequency) component
                var tf = CalculateTF(termFrequency, docLength, avgDocLength);

                // Add to score
                score += idf * tf;
            }

            scores[doc.Id] = score;
        }

        return scores;
    }

    /// <summary>
    /// Calculate Inverse Document Frequency
    /// </summary>
    private double CalculateIDF(int totalDocs, int docFrequency)
    {
        if (_parameters.UseBM25Plus)
        {
            // BM25+ variant: adds delta to prevent negative IDF
            return Math.Log((totalDocs - docFrequency + 0.5) / (docFrequency + 0.5)) + _parameters.Delta;
        }
        else
        {
            // Standard BM25 IDF
            return Math.Log((totalDocs - docFrequency + 0.5) / (docFrequency + 0.5));
        }
    }

    /// <summary>
    /// Calculate Term Frequency component
    /// </summary>
    private double CalculateTF(int termFrequency, int docLength, double avgDocLength)
    {
        if (!_parameters.UseLengthNormalization)
        {
            // Without length normalization
            return (termFrequency * (_parameters.K1 + 1)) / (termFrequency + _parameters.K1);
        }

        // With length normalization
        var lengthNormalization = 1 - _parameters.B + _parameters.B * (docLength / avgDocLength);
        return (termFrequency * (_parameters.K1 + 1)) / (termFrequency + _parameters.K1 * lengthNormalization);
    }

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
                    match = match && (tags.ContainsKey(condition.Key) && tags[condition.Key].Contains(condition.Value[index]));
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
