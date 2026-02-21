// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AI.KnowledgeBase.MemoryStorage.Keyword;
using Microsoft.Extensions.Logging;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.AI;
using Microsoft.KernelMemory.Diagnostics;
using Microsoft.KernelMemory.FileSystem.DevTools;
using Microsoft.KernelMemory.MemoryStorage;

namespace AI.KnowledgeBase.MemoryStorage.Hybrid;

/// <summary>
/// Hybrid memory database that combines semantic (vector) search and keyword (BM25) search.
/// Performs parallel queries and fuses scores with configurable weights.
/// </summary>
public class HybridMemoryDb : MemoryDbBase
{
    private readonly ITextEmbeddingGenerator _embeddingGenerator;
    private readonly BM25Algorithm _bm25Algorithm;
    private readonly double _semanticWeight;
    private readonly double _keywordWeight;

    /// <summary>
    /// Create new instance with default weights (70% semantic, 30% keyword)
    /// </summary>
    /// <param name="config">Hybrid memory db settings</param>
    /// <param name="embeddingGenerator">Text embedding generator for semantic search</param>
    /// <param name="loggerFactory">Application logger factory</param>
    public HybridMemoryDb(
        HybridMemoryDbConfig config,
        ITextEmbeddingGenerator embeddingGenerator,
        ILoggerFactory? loggerFactory = null)
        : this(config, embeddingGenerator, new BM25Parameters(), 0.7, 0.3, loggerFactory)
    {
    }

    /// <summary>
    /// Create new instance with custom weights
    /// </summary>
    /// <param name="config">Hybrid memory db settings</param>
    /// <param name="embeddingGenerator">Text embedding generator for semantic search</param>
    /// <param name="semanticWeight">Weight for semantic search scores (0.0 to 1.0)</param>
    /// <param name="keywordWeight">Weight for keyword search scores (0.0 to 1.0)</param>
    /// <param name="loggerFactory">Application logger factory</param>
    public HybridMemoryDb(
        HybridMemoryDbConfig config,
        ITextEmbeddingGenerator embeddingGenerator,
        double semanticWeight,
        double keywordWeight,
        ILoggerFactory? loggerFactory = null)
        : this(config, embeddingGenerator, new BM25Parameters(), semanticWeight, keywordWeight, loggerFactory)
    {
    }

    /// <summary>
    /// Create new instance with custom BM25 parameters and weights
    /// </summary>
    /// <param name="config">Hybrid memory db settings</param>
    /// <param name="embeddingGenerator">Text embedding generator for semantic search</param>
    /// <param name="bm25Parameters">BM25 algorithm parameters for keyword search</param>
    /// <param name="semanticWeight">Weight for semantic search scores (0.0 to 1.0)</param>
    /// <param name="keywordWeight">Weight for keyword search scores (0.0 to 1.0)</param>
    /// <param name="loggerFactory">Application logger factory</param>
    public HybridMemoryDb(
        HybridMemoryDbConfig config,
        ITextEmbeddingGenerator embeddingGenerator,
        BM25Parameters bm25Parameters,
        double semanticWeight = 0.7,
        double keywordWeight = 0.3,
        ILoggerFactory? loggerFactory = null)
        : base(config, (loggerFactory ?? DefaultLogger.Factory).CreateLogger<HybridMemoryDb>())
    {
        this._embeddingGenerator = embeddingGenerator ?? throw new ArgumentNullException(nameof(embeddingGenerator));
        this._bm25Algorithm = new BM25Algorithm(bm25Parameters);
        
        // Validate and normalize weights
        if (semanticWeight < 0 || semanticWeight > 1)
            throw new ArgumentException("Semantic weight must be between 0.0 and 1.0", nameof(semanticWeight));
        if (keywordWeight < 0 || keywordWeight > 1)
            throw new ArgumentException("Keyword weight must be between 0.0 and 1.0", nameof(keywordWeight));
        
        // Normalize weights to sum to 1.0
        double totalWeight = semanticWeight + keywordWeight;
        if (totalWeight == 0)
        {
            _semanticWeight = 0.5;
            _keywordWeight = 0.5;
        }
        else
        {
            _semanticWeight = semanticWeight / totalWeight;
            _keywordWeight = keywordWeight / totalWeight;
        }
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
        
        this._log.LogDebug("{RecordCount} records loaded for hybrid similarity check", records.Count);

        if (records.Count == 0)
        {
            yield break;
        }

        // Perform parallel semantic and keyword searches
        var semanticTask = PerformSemanticSearchAsync(records, text, cancellationToken);
        var keywordTask = PerformKeywordSearchAsync(records, text, cancellationToken);

        await Task.WhenAll(semanticTask, keywordTask).ConfigureAwait(false);

        var semanticScores = await semanticTask;
        var keywordScores = await keywordTask;

        // Fuse scores using weighted combination
        var fusedScores = FuseScores(semanticScores, keywordScores);

        // Sort by fused score descending and filter by minRelevance
        var sortedResults = fusedScores
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
    /// Perform semantic (vector) search
    /// </summary>
    private async Task<Dictionary<string, double>> PerformSemanticSearchAsync(
        Dictionary<string, MemoryRecord> records,
        string text,
        CancellationToken cancellationToken)
    {
        var scores = new Dictionary<string, double>();
        
        try
        {
            // Generate embedding for the query text
            Embedding textEmbedding = await this._embeddingGenerator.GenerateEmbeddingAsync(text, cancellationToken).ConfigureAwait(false);
            
            // Calculate cosine similarity for each record
            foreach (var record in records)
            {
                if (record.Value.Vector != null && record.Value.Vector.Length > 0)
                {
                    scores[record.Key] = textEmbedding.CosineSimilarity(record.Value.Vector);
                }
                else
                {
                    scores[record.Key] = 0.0;
                }
            }
        }
        catch (Exception ex)
        {
            this._log.LogError(ex, "Error performing semantic search");
            // If semantic search fails, assign zero scores
            foreach (var record in records)
            {
                scores[record.Key] = 0.0;
            }
        }

        return scores;
    }

    /// <summary>
    /// Perform keyword (BM25) search
    /// </summary>
    private async Task<Dictionary<string, double>> PerformKeywordSearchAsync(
        Dictionary<string, MemoryRecord> records,
        string text,
        CancellationToken cancellationToken)
    {
        var scores = new Dictionary<string, double>();
        
        try
        {
            // Prepare documents for BM25 calculation
            var documents = PrepareDocuments(records);
            if (documents.Count == 0)
            {
                // If no valid documents, assign zero scores
                foreach (var record in records)
                {
                    scores[record.Key] = 0.0;
                }
                return scores;
            }

            // Calculate BM25 scores
            var bm25Scores = _bm25Algorithm.CalculateScores(documents, text);
            bm25Scores = BM25Normalizer.NormalizeScores(bm25Scores);

            // Map BM25 scores back to record IDs
            foreach (var record in records)
            {
                if (bm25Scores.TryGetValue(record.Key, out var score))
                {
                    scores[record.Key] = score;
                }
                else
                {
                    scores[record.Key] = 0.0;
                }
            }
        }
        catch (Exception ex)
        {
            this._log.LogError(ex, "Error performing keyword search");
            // If keyword search fails, assign zero scores
            foreach (var record in records)
            {
                scores[record.Key] = 0.0;
            }
        }

        return scores;
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
    /// Fuse semantic and keyword scores using weighted combination
    /// </summary>
    private Dictionary<string, double> FuseScores(
        Dictionary<string, double> semanticScores,
        Dictionary<string, double> keywordScores)
    {
        var fusedScores = new Dictionary<string, double>();
        
        // Get all unique record IDs from both score sets
        var allIds = semanticScores.Keys.Union(keywordScores.Keys).Distinct();
        
        foreach (var id in allIds)
        {
            double semanticScore = semanticScores.TryGetValue(id, out var sScore) ? sScore : 0.0;
            double keywordScore = keywordScores.TryGetValue(id, out var kScore) ? kScore : 0.0;
            
            // Apply weighted combination
            double fusedScore = (semanticScore * _semanticWeight) + (keywordScore * _keywordWeight);
            fusedScores[id] = fusedScore;
        }

        return fusedScores;
    }

    /// <summary>
    /// Get the current weight configuration
    /// </summary>
    public (double SemanticWeight, double KeywordWeight) GetWeights() => (_semanticWeight, _keywordWeight);

    /// <summary>
    /// Get the BM25 algorithm instance for advanced usage
    /// </summary>
    public BM25Algorithm GetBM25Algorithm() => _bm25Algorithm;
}
