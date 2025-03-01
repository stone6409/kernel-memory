// Copyright (c) Microsoft. All rights reserved.

using Microsoft.KernelMemory;
using Microsoft.KernelMemory.AI.Ollama;
using Microsoft.KernelMemory.Configuration;
using Microsoft.KernelMemory.DocumentStorage.DevTools;
using Microsoft.KernelMemory.FileSystem.DevTools;
using Microsoft.KernelMemory.MemoryStorage.DevTools;

namespace VectorDbDemo;

public static class Program
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2007:Consider calling ConfigureAwait on the awaited task", Justification = "<Pending>")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA1303:Do not pass literals as localized parameters", Justification = "<Pending>")]
    public static async Task Main(string[] args)
    {
        // Partition input text in chunks of 100 tokens
        const int Chunksize = 100;

        // Search settings
        const string Query = "astrobiology";
        const float MinRelevance = 0.7f;
        const int Limit = 2;

        var ollamaConfig = new OllamaConfig()
        {
            EmbeddingModel = new OllamaModelConfig("nomic-embed-text:latest") { MaxTokenTotal = 2048 },
            Endpoint = "http://localhost:11434/"
        };

        // Customize memory records size (in tokens)
        var textPartitioningOptions = new TextPartitioningOptions
        {
            MaxTokensPerParagraph = Chunksize,
            OverlappingTokens = 0,
        };

        var memory = new KernelMemoryBuilder()
           .WithOllamaTextEmbeddingGeneration(ollamaConfig)
           .WithCustomTextPartitioningOptions(textPartitioningOptions)
           .WithSimpleFileStorage(new SimpleFileStorageConfig { StorageType = FileSystemTypes.Disk })
           .WithSimpleVectorDb(new SimpleVectorDbConfig { StorageType = FileSystemTypes.Disk })
           .WithoutTextGenerator()
           .Build();

        // Load text into memory
        Console.WriteLine("Importing memories...");
        await memory.ImportDocumentAsync(filePath: "story.docx", documentId: "example207");

        // Search
        Console.WriteLine("Searching memories...");
        // 关键点：搜索
        SearchResult relevant = await memory.SearchAsync(query: Query, minRelevance: MinRelevance, limit: Limit);
        Console.WriteLine($"Relevant documents: {relevant.Results.Count}");
    }
}
