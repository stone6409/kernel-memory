using Microsoft.KernelMemory;
using Microsoft.KernelMemory.AI.Ollama;
using Microsoft.KernelMemory.Configuration;
using Microsoft.KernelMemory.DocumentStorage.DevTools;
using Microsoft.KernelMemory.FileSystem.DevTools;
using Microsoft.KernelMemory.MemoryStorage.DevTools;

namespace VectorDbDemo;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2007:Consider calling ConfigureAwait on the awaited task", Justification = "<Pending>")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA1303:Do not pass literals as localized parameters", Justification = "<Pending>")]
public class RAGService
{
    private readonly IKernelMemory _memory;
    private readonly string _storageFolder;

    public RAGService(string storageFolder, OllamaConfig ollamaConfig, int chunkSize = 200)
    {
        _storageFolder = storageFolder;

        // 配置文本分块
        var textPartitioningOptions = new TextPartitioningOptions
        {
            MaxTokensPerParagraph = chunkSize,
            OverlappingTokens = 0,
        };

        // 配置存储
        var storageConfig = new SimpleFileStorageConfig
        {
            Directory = _storageFolder,
            StorageType = FileSystemTypes.Disk,
        };

        // 配置向量数据库
        var vectorDbConfig = new SimpleVectorDbConfig
        {
            Directory = _storageFolder,
            StorageType = FileSystemTypes.Disk,
        };

        // 配置搜索客户端
        var searchClientConfig = new SearchClientConfig
        {
            AnswerTokens = 4096,
        };

        // 构建 KernelMemory 实例
        _memory = new KernelMemoryBuilder()
            .WithOllamaTextEmbeddingGeneration(ollamaConfig)
            .WithCustomTextPartitioningOptions(textPartitioningOptions)
            .WithSimpleFileStorage(storageConfig)
            .WithSimpleVectorDb(vectorDbConfig)
            .WithSearchClientConfig(searchClientConfig)
            .WithoutTextGenerator()
            .Build();
    }

    /// <summary>
    /// 导入文档到内存
    /// </summary>
    /// <param name="filePath">文档路径</param>
    /// <param name="documentId">文档 ID</param>
    /// <returns>文档 ID</returns>
    public async Task<string> ImportDocumentAsync(string filePath, string documentId, string? index = null)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("Document file not found.", filePath);
        }

        Console.WriteLine("Importing doucement...");
        var docId = await _memory.ImportDocumentAsync(filePath, documentId, null, index);
        Console.WriteLine($"- Document Id: {docId}");
        return docId;
    }

    /// <summary>
    /// 搜索相关文档
    /// </summary>
    /// <param name="query">查询内容</param>
    /// <param name="minRelevance">最小相关性</param>
    /// <param name="limit">返回结果数量</param>
    /// <returns>搜索结果</returns>
    public async Task<SearchResult> SearchAsync(string query, string? index = null, float minRelevance = 0.4f, int limit = 2)
    {
        Console.WriteLine("Searching doucement...");
        var result = await _memory.SearchAsync(query, index: index, minRelevance: minRelevance, limit: limit);
        return result;
    }

    /// <summary>
    /// 打印搜索结果
    /// </summary>
    /// <param name="result">搜索结果</param>
    public void PrintSearchResult(SearchResult result)
    {
        Console.WriteLine($"Relevant documents: {result.Results.Count}");

        foreach (Citation citation in result.Results)
        {
            Console.WriteLine($"Document ID: {citation.DocumentId}");
            Console.WriteLine($"Relevant partitions: {citation.Partitions.Count}");

            foreach (Citation.Partition partition in citation.Partitions)
            {
                Console.WriteLine($" * Partition {partition.PartitionNumber}, relevance: {partition.Relevance}");
            }

            Console.WriteLine("--------------------------");

            // 打印每个段落的内容
            foreach (Citation.Partition partition in citation.Partitions)
            {
                Console.WriteLine($"# Partition {partition.PartitionNumber}");
                Console.WriteLine(partition.Text);
                Console.WriteLine();
            }

            Console.WriteLine("--------------------------");
        }
    }

    /// <summary>
    /// 导入指定文件夹下的所有 .cs 和 .xaml 文件，支持包含和排除文件集合
    /// </summary>
    /// <param name="folderPath">文件夹路径</param>
    /// <param name="includePatterns">包含文件的模式集合（例如：*.cs, *.xaml）</param>
    /// <param name="excludePaths">排除文件的路径集合（例如：SubFolder\File.cs）</param>
    /// <returns>导入的文件数量</returns>
    public async Task<int> ImportDocumentsFromFolderAsync(string folderPath, IEnumerable<string> includePatterns, IEnumerable<string> excludePaths = null, string? index = null)
    {
        if (!Directory.Exists(folderPath))
        {
            throw new DirectoryNotFoundException($"Folder not found: {folderPath}");
        }

        // 获取所有符合条件的文件
        var files = includePatterns
            .SelectMany(pattern => Directory.GetFiles(folderPath, pattern, SearchOption.AllDirectories))
            .Distinct()
            .ToList();

        // 如果存在排除文件集合，过滤掉排除的文件
        if (excludePaths != null && excludePaths.Any())
        {
            files = files
                .Where(file => !excludePaths.Any(exclude => file.EndsWith(exclude, StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }

        // 逐个导入文件
        foreach (var file in files)
        {
            // 获取文件相对于 folderPath 的相对路径作为 documentId
            var documentId = Path.GetRelativePath(folderPath, file).Replace("\\", "_");
            await ImportDocumentAsync(file, documentId, index);
        }

        return files.Count;
    }

    /// <summary>
    /// 获取所有索引的名称
    /// </summary>
    /// <returns>索引名称列表</returns>
    public async Task<IEnumerable<string>> ListIndexesAsync()
    {
        var indexes = await _memory.ListIndexesAsync();
        return indexes.Select(index => index.Name);
    }
}
