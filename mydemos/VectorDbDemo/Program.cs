using Microsoft.KernelMemory.AI.Ollama;
using System;
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

            if (!StorageExists)
            {
                // 导入文档
                await ragService.ImportDocumentAsync(filePath: "Data/Persons.txt", documentId: "example001");
                await ragService.ImportDocumentAsync(filePath: "Data/巴菲特投资名言.docx", documentId: "example002");

                // 定义包含和排除的文件集合
                var includePatterns = new[] { "*.cs", "*.xaml" };
                var excludePaths = new[] { "SubFolder\\File.cs" };

                // 导入指定文件夹下的所有符合条件的文件
                var importCount = await ragService.ImportDocumentsFromFolderAsync("D:/src/Fork/kernel-memory/mydemos/VectorDbDemo", includePatterns, excludePaths);
                Console.WriteLine($"Imported {importCount} files.");
            }

            while (true)
            {
                Console.WriteLine("Please enter your question (type 'Exit' to exit):");
                string userInput = Console.ReadLine();

                if (userInput == "Exit")
                    break;

                if (!string.IsNullOrWhiteSpace(userInput))
                {
                    var result = await ragService.SearchAsync(userInput);
                    ragService.PrintSearchResult(result);
                }
            }
        }
    }
}
