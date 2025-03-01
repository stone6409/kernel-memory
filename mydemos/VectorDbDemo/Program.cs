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
    private static string StorageFolder => Path.GetFullPath($"./dbstorage");
    private static bool StorageExists => Directory.Exists(StorageFolder) && Directory.GetDirectories(StorageFolder).Length > 0;

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2007:Consider calling ConfigureAwait on the awaited task", Justification = "<Pending>")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA1303:Do not pass literals as localized parameters", Justification = "<Pending>")]
    public static async Task Main(string[] args)
    {
        // Partition input text in chunks of 100 tokens
        const int Chunksize = 100;

        // Search settings
        const float MinRelevance = 0.4f;
        const int Limit = 2;

        var ollamaConfig = new OllamaConfig()
        {
            //EmbeddingModel = new OllamaModelConfig("nomic-embed-text:latest") { MaxTokenTotal = 2048 },
            //EmbeddingModel = new OllamaModelConfig("mxbai-embed-large") { MaxTokenTotal = 2048 },
            EmbeddingModel = new OllamaModelConfig("bge-m3") { MaxTokenTotal = 2048 },
            Endpoint = "http://localhost:11434/"
        };

        SimpleFileStorageConfig storageConfig = new()
        {
            Directory = StorageFolder,
            StorageType = FileSystemTypes.Disk,
        };

        SimpleVectorDbConfig vectorDbConfig = new()
        {
            Directory = StorageFolder,
            StorageType = FileSystemTypes.Disk,
        };

        // Customize memory records size (in tokens)
        var textPartitioningOptions = new TextPartitioningOptions
        {
            MaxTokensPerParagraph = Chunksize,
            OverlappingTokens = 0,
        };

        SearchClientConfig searchClientConfig = new()
        {
            AnswerTokens = 4096,
        };

        var memory = new KernelMemoryBuilder()
           .WithOllamaTextEmbeddingGeneration(ollamaConfig)
           .WithCustomTextPartitioningOptions(textPartitioningOptions)
           .WithSimpleFileStorage(storageConfig)
           .WithSimpleVectorDb(vectorDbConfig)
           .WithSearchClientConfig(searchClientConfig)
           .WithoutTextGenerator()
           .Build();

        if (!StorageExists)
        {
            // Load text into memory
            Console.WriteLine("Importing memories...");
            await memory.ImportDocumentAsync(filePath: "巴菲特投资名言.docx", documentId: "example207");
        }      

        // Search
        Console.WriteLine("Searching memories...");

        string userInput = "贪婪";
        while (userInput != "Exit")
        {
            if (string.IsNullOrWhiteSpace(userInput))
            {
                continue;
            }

            // 关键点：搜索
            SearchResult relevant = await memory.SearchAsync(query: userInput, minRelevance: MinRelevance, limit: Limit);
            PrintResult(relevant);

            Console.WriteLine("Please Ask your question");
            userInput = Console.ReadLine();
        }
    }

    public static void PrintResult(SearchResult relevant)
    {
        Console.WriteLine($"Relevant documents: {relevant.Results.Count}");

        foreach (Citation result in relevant.Results)
        {
            // Store the document IDs so we can load all their records later
            Console.WriteLine($"Document ID: {result.DocumentId}");
            Console.WriteLine($"Relevant partitions: {result.Partitions.Count}");
            foreach (Citation.Partition partition in result.Partitions)
            {
                Console.WriteLine($" * Partition {partition.PartitionNumber}, relevance: {partition.Relevance}");
            }

            Console.WriteLine("--------------------------");

            // For each relevant partition fetch the partition before and one after
            foreach (Citation.Partition partition in result.Partitions)
            {
                // Collect partitions in a sorted collection
                var partitions = new SortedDictionary<int, Citation.Partition> { [partition.PartitionNumber] = partition };

                // Filters to fetch adjacent partitions
                //var filters = new List<MemoryFilter>
                //{
                //    MemoryFilters.ByDocument(result.DocumentId).ByTag(Constants.ReservedFilePartitionNumberTag, $"{partition.PartitionNumber - 1}"),
                //    MemoryFilters.ByDocument(result.DocumentId).ByTag(Constants.ReservedFilePartitionNumberTag, $"{partition.PartitionNumber + 1}")
                //};

                //// Fetch adjacent partitions and add them to the sorted collection
                //SearchResult adjacentList = await memory.SearchAsync("", filters: filters, limit: 2);
                //foreach (Citation.Partition adjacent in adjacentList.Results.First().Partitions)
                //{
                //    partitions[adjacent.PartitionNumber] = adjacent;
                //}

                // Print partitions in order
                foreach (var p in partitions)
                {
                    Console.WriteLine($"# Partition {p.Value.PartitionNumber}");
                    Console.WriteLine(p.Value.Text);
                    Console.WriteLine();
                }

                Console.WriteLine("--------------------------");
            }

            Console.WriteLine();
        }
    }
}
