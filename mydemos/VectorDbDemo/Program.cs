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
                await ragService.ImportDocumentAsync(filePath: "巴菲特投资名言.docx", documentId: "example207");
            }

            // 搜索
            Console.WriteLine("请输入您的问题 (输入 'Exit' 退出):");
            string userInput = Console.ReadLine();

            while (userInput != "Exit")
            {
                if (!string.IsNullOrWhiteSpace(userInput))
                {
                    var result = await ragService.SearchAsync(userInput);
                    ragService.PrintSearchResult(result);
                }

                Console.WriteLine("请输入您的问题 (输入 'Exit' 退出):");
                userInput = Console.ReadLine();
            }
        }
    }
}
