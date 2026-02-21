// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace AI.KnowledgeBase.MemoryStorage.Keyword;

/// <summary>
/// BM25 algorithm implementation for text similarity scoring
/// </summary>
public class BM25Algorithm
{
    private readonly BM25Parameters _parameters;

    /// <summary>
    /// Initialize BM25 algorithm with default parameters
    /// </summary>
    public BM25Algorithm() : this(new BM25Parameters())
    {
    }

    /// <summary>
    /// Initialize BM25 algorithm with custom parameters
    /// </summary>
    /// <param name="parameters">BM25 algorithm parameters</param>
    public BM25Algorithm(BM25Parameters parameters)
    {
        _parameters = parameters ?? new BM25Parameters();
    }

    /// <summary>
    /// Calculate BM25 scores for documents against a query
    /// </summary>
    /// <param name="documents">List of documents to score</param>
    /// <param name="query">Query text</param>
    /// <returns>Dictionary of document IDs to BM25 scores</returns>
    public Dictionary<string, double> CalculateScores(List<BM25Document> documents, string query)
    {
        if (documents == null || documents.Count == 0)
        {
            return new Dictionary<string, double>();
        }

        var queryTokens = TokenizeText(query);
        if (queryTokens.Count == 0)
        {
            return new Dictionary<string, double>();
        }

        return CalculateScores(documents, queryTokens);
    }

    /// <summary>
    /// Calculate BM25 scores for documents against tokenized query
    /// </summary>
    /// <param name="documents">List of documents to score</param>
    /// <param name="queryTokens">Tokenized query terms</param>
    /// <returns>Dictionary of document IDs to BM25 scores</returns>
    public Dictionary<string, double> CalculateScores(List<BM25Document> documents, List<string> queryTokens)
    {
        var scores = new Dictionary<string, double>();
        var N = documents.Count; // Total number of documents
        var avgDocLength = documents.Average(d => d.Tokens.Count);

        // Calculate document frequency for each query term
        var docFrequencies = CalculateDocumentFrequencies(documents, queryTokens);

        // Calculate BM25 score for each document
        foreach (var doc in documents)
        {
            var docLength = doc.Tokens.Count;
            var score = 0.0;

            foreach (var term in queryTokens.Distinct())
            {
                //var termFrequency = doc.Tokens.Count(t => t == term);
                var termFrequency = doc.Tokens.Count(t => t.Contains(term));
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
    /// Tokenize text into terms
    /// </summary>
    /// <param name="text">Input text</param>
    /// <returns>List of tokenized terms</returns>
    public List<string> TokenizeText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new List<string>();
        }

        // Use Unicode-aware tokenization to support multiple languages
        var tokens = Regex.Replace(text, @"[^\p{L}0-9_]+", " ")
            .Split(' ')
            .Select(x => x.Trim().ToLowerInvariant())
            .Where(x => !string.IsNullOrEmpty(x) && x.Length > 1) // Filter out single characters
            .ToList();

        return tokens;
    }

    /// <summary>
    /// Calculate document frequencies for query terms
    /// </summary>
    private Dictionary<string, int> CalculateDocumentFrequencies(List<BM25Document> documents, List<string> queryTokens)
    {
        var docFrequencies = new Dictionary<string, int>();
        var distinctQueryTerms = queryTokens.Distinct();

        foreach (string? term in distinctQueryTerms)
        {
            //docFrequencies[term] = documents.Count(d => d.Tokens.Contains(term));
            docFrequencies[term] = documents.Count(d => d.Tokens.Any(t => t.Contains(term)));
        }

        return docFrequencies;
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
            return termFrequency * (_parameters.K1 + 1) / (termFrequency + _parameters.K1);
        }

        // With length normalization
        var lengthNormalization = 1 - _parameters.B + _parameters.B * (docLength / avgDocLength);
        return termFrequency * (_parameters.K1 + 1) / (termFrequency + _parameters.K1 * lengthNormalization);
    }

    /// <summary>
    /// Get the current BM25 parameters
    /// </summary>
    public BM25Parameters GetParameters() => _parameters;

    /// <summary>
    /// Create document from text
    /// </summary>
    public BM25Document CreateDocument(string id, string text, object? metadata = null)
    {
        return new BM25Document
        {
            Id = id,
            Text = text,
            Tokens = TokenizeText(text),
            Metadata = metadata
        };
    }

    /// <summary>
    /// Batch create documents from texts
    /// </summary>
    public List<BM25Document> CreateDocuments(Dictionary<string, string> idTextPairs, object? metadata = null)
    {
        var documents = new List<BM25Document>();

        foreach (var pair in idTextPairs)
        {
            documents.Add(CreateDocument(pair.Key, pair.Value, metadata));
        }

        return documents;
    }
}
