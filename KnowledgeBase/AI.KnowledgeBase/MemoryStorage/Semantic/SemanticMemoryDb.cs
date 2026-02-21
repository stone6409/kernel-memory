// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.AI;
using Microsoft.KernelMemory.Diagnostics;
using Microsoft.KernelMemory.FileSystem.DevTools;
using Microsoft.KernelMemory.MemoryStorage;

namespace AI.KnowledgeBase.MemoryStorage.Vector;

/// <summary>
/// Basic vector db implementation, designed for tests and demos only.
/// When searching, uses brute force comparing against all stored records.
/// </summary>
public class SemanticMemoryDb : MemoryDbBase
{
    private readonly ITextEmbeddingGenerator _embeddingGenerator;

    /// <summary>
    /// Create new instance
    /// </summary>
    /// <param name="config">Simple vector db settings</param>
    /// <param name="embeddingGenerator">Text embedding generator</param>
    /// <param name="loggerFactory">Application logger factory</param>
    public SemanticMemoryDb(
        SemanticMemoryDbConfig config,
        ITextEmbeddingGenerator embeddingGenerator,
        ILoggerFactory? loggerFactory = null)
        : base(config, (loggerFactory ?? DefaultLogger.Factory).CreateLogger<SemanticMemoryDb>())
    {
        this._embeddingGenerator = embeddingGenerator ?? throw new ArgumentNullException(nameof(embeddingGenerator));
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
        
        this._log.LogDebug("{VectorCount} vectors loaded for similarity check", records.Count);

        if (records.Count == 0)
        {
            yield break;
        }

        // Calculate all the distances from the given vector
        // Note: this is a brute force search, very slow, not meant for production use cases
        var similarity = new Dictionary<string, double>();
        Embedding textEmbedding = await this._embeddingGenerator.GenerateEmbeddingAsync(text, cancellationToken).ConfigureAwait(false);
        
        foreach (var record in records)
        {
            similarity[record.Value.Id] = textEmbedding.CosineSimilarity(record.Value.Vector);
        }

        // Sort distances, from closest to most distant, and filter out irrelevant results
        var sortedResults = similarity
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
}
