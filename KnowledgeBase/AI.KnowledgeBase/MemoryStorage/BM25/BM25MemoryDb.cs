// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
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
public class BM25MemoryDb : MemoryDbBase
{
    private readonly BM25Algorithm _bm25Algorithm;

    /// <summary>
    /// Create new instance
    /// </summary>
    /// <param name="config">Simple BM25 db settings</param>
    /// <param name="loggerFactory">Application logger factory</param>
    public BM25MemoryDb(
        BM25MemoryDbConfig config,
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
    public BM25MemoryDb(
        BM25MemoryDbConfig config,
        BM25Parameters parameters,
        ILoggerFactory? loggerFactory = null)
        : base(config, (loggerFactory ?? DefaultLogger.Factory).CreateLogger<BM25MemoryDb>())
    {
        this._bm25Algorithm = new BM25Algorithm(parameters);
    }

    /// <inheritdoc />
    public override async IAsyncEnumerable<(MemoryRecord, double)> GetSimilarListAsync(
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
        var records = await LoadFilteredRecordsAsync(index, filters, withEmbeddings, cancellationToken);
        
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
}
