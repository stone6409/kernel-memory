using AI.KnowledgeBase.Configuration;
using AI.KnowledgeBase.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace KnowledgeBaseDemo;

public static class Program
{
    private static string StorageFolder => Path.GetFullPath($"./dbstorage");
    private static bool StorageExists => Directory.Exists(StorageFolder) && Directory.GetDirectories(StorageFolder).Length > 0;

    public static async Task Main(string[] args)
    {
        // 创建并配置主机
        var host = CreateHostBuilder(args).Build();

        // 获取配置服务
        var configuration = host.Services.GetRequiredService<IConfiguration>();

        // 使用TencentRAGService，传入配置
        var ragService = new TencentRAGService(StorageFolder, configuration);

        // 定义并导入索引
        await DefineAndImportIndexesAsync(ragService);

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
                var result = await ragService.SearchAsync(userInput);
                ragService.PrintSearchResult(result);
            }
        }
    }

    /// <summary>
    /// 创建主机构建器
    /// </summary>
    private static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((context, config) =>
            {
                // 添加appsettings.json文件
                //config.SetBasePath(Directory.GetCurrentDirectory());
                config.SetBasePath(@"D:\src\ScTrials\src\AI\KnowledgeBase\KnowledgeBaseDemo\bin\Debug\net9.0");
                config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

                // 添加环境变量
                config.AddEnvironmentVariables();

                // 添加命令行参数
                if (args != null)
                {
                    config.AddCommandLine(args);
                }
            })
            .ConfigureServices((context, services) =>
            {
                // 注册配置
                services.AddSingleton(context.Configuration);
            });

    /// <summary>
    /// 定义索引配置并导入文档
    /// </summary>
    /// <param name="ragService">RAGService 实例</param>
    private static async Task DefineAndImportIndexesAsync(IRAGService ragService)
    {
        // 定义多个索引及其对应的导入配置
        var indexConfigs = new List<IndexConfig>
        {
            new()
            {
                IndexName = "default",
                SingleFiles = new List<SingleFileConfig>
                {
                    new() { FilePath = "Data/Persons.txt", DocumentId = "example001" },
                    new() { FilePath = "Data/巴菲特投资名言.docx", DocumentId = "example002" }
                },
            },
            //new()
            //{
            //    IndexName = "StoneToolkit",
            //    FolderConfigs = new List<FolderConfig>
            //    {
            //        new() {
            //            FolderPath = @"D:\src\ScTrials\src\StoneToolkit\StoneToolkit.Common",
            //            IncludePatterns = new[] { "*.cs", "*.xaml" },
            //            ExcludeFolders = new[] { "bin", "obj" } // 排除 bin 和 obj 文件夹
            //        },
            //        new() {
            //            FolderPath = @"D:\src\ScTrials\src\StoneToolkit\StoneToolkit.WpfCommon",
            //            IncludePatterns = new[] { "*.cs", "*.xaml" },
            //            ExcludeFolders = new[] { "bin", "obj" } // 排除 bin 和 obj 文件夹
            //        }
            //    }
            //}
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
    }

    /// <summary>
    /// 导入文档的方法
    /// </summary>
    /// <param name="ragService">RAGService 实例</param>
    /// <param name="config">索引配置</param>
    private static async Task ImportDocumentsAsync(IRAGService ragService, IndexConfig config)
    {
        // 导入单个文件
        if (config.SingleFiles != null)
        {
            foreach (var fileConfig in config.SingleFiles)
            {
                await ragService.ImportDocumentAsync(fileConfig.FilePath, fileConfig.DocumentId, config.IndexName);
            }
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
                    folderConfig.ExcludeFolders,
                    config.IndexName);
                Console.WriteLine($"Imported {importCount} files from '{folderConfig.FolderPath}' to index '{config.IndexName}'.");
            }
        }
    }
}
