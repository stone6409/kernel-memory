using Microsoft.KernelMemory.AI.Ollama;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace VectorDbDemo
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2007:Consider calling ConfigureAwait on the awaited task", Justification = "<Pending>")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA1303:Do not pass literals as localized parameters", Justification = "<Pending>")]
    public static class Program
    {
        private static string StorageFolder => Path.GetFullPath($"./dbstorage");
        private static bool StorageExists => Directory.Exists(StorageFolder) && Directory.GetDirectories(StorageFolder).Length > 0;

        public static async Task Main(string[] args)
        {
            var ollamaConfig = new OllamaConfig
            {
                EmbeddingModel = new OllamaModelConfig("bge-m3") { MaxTokenTotal = 2048 },
                Endpoint = "http://localhost:11434/"
            };

            var ragService = new RAGService(StorageFolder, ollamaConfig);

            // 定义多个索引及其对应的导入配置
            var indexConfigs = new List<IndexConfig>
            {
                new IndexConfig
                {
                    IndexName = "StoneToolkit",
                    SingleFiles = new List<SingleFileConfig>
                    {
                        new SingleFileConfig { FilePath = "Data/Persons.txt", DocumentId = "example001" },
                        new SingleFileConfig { FilePath = "Data/巴菲特投资名言.docx", DocumentId = "example002" }
                    },
                    FolderConfigs = new List<FolderConfig>
                    {
                        new FolderConfig
                        {
                            FolderPath = @"D:\src\ScTrials\src\StoneToolkit\StoneToolkit.Common",
                            IncludePatterns = new[] { "*.cs", "*.xaml" },
                            ExcludePaths = new[] { @"SubFolder\File.cs" }
                        },
                    }
                },
            };

            // 检查并导入每个索引
            foreach (var config in indexConfigs)
            {
                var indexes = await ragService.ListIndexesAsync();
                if (!indexes.Contains(config.IndexName))
                {
                    Console.WriteLine($"Index '{config.IndexName}' does not exist. Importing documents...");
                    await ImportDocumentsAsync(ragService, config);
                }
                else
                {
                    Console.WriteLine($"Index '{config.IndexName}' already exists. Skipping import.");
                }
            }

            // 交互式搜索
            while (true)
            {
                Console.WriteLine("Please enter your question (type 'Exit' to exit):");
                string userInput = Console.ReadLine();

                if (userInput == "Exit")
                    break;

                if (!string.IsNullOrWhiteSpace(userInput))
                {
                    // 默认在第一个索引中搜索
                    var result = await ragService.SearchAsync(userInput, index: indexConfigs[0].IndexName);
                    ragService.PrintSearchResult(result);
                }
            }
        }

        /// <summary>
        /// 导入文档的方法
        /// </summary>
        /// <param name="ragService">RAGService 实例</param>
        /// <param name="config">索引配置</param>
        private static async Task ImportDocumentsAsync(RAGService ragService, IndexConfig config)
        {
            // 导入单个文件
            foreach (var fileConfig in config.SingleFiles)
            {
                await ragService.ImportDocumentAsync(fileConfig.FilePath, fileConfig.DocumentId, config.IndexName);
            }

            // 导入多个文件夹中的文件
            if (config.FolderConfigs != null)
            {
                foreach (var folderConfig in config.FolderConfigs)
                {
                    var importCount = await ragService.ImportDocumentsFromFolderAsync(
                        folderConfig.FolderPath,
                        folderConfig.IncludePatterns,
                        folderConfig.ExcludePaths,
                        config.IndexName);
                    Console.WriteLine($"Imported {importCount} files from '{folderConfig.FolderPath}' to index '{config.IndexName}'.");
                }
            }
        }
    }
}
