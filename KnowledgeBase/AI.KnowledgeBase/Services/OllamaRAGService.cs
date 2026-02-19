using Microsoft.KernelMemory;
using Microsoft.KernelMemory.AI.Ollama;
using Microsoft.KernelMemory.Configuration;
using Microsoft.KernelMemory.DocumentStorage.DevTools;
using Microsoft.KernelMemory.FileSystem.DevTools;
using Microsoft.KernelMemory.MemoryStorage.DevTools;
using System;

namespace AI.KnowledgeBase.Services
{
    /// <summary>
    /// 基于Ollama的RAG服务实现
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2007:Consider calling ConfigureAwait on the awaited task", Justification = "<Pending>")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA1303:Do not pass literals as localized parameters", Justification = "<Pending>")]
    public class OllamaRAGService : RAGServiceBase
    {
        private readonly OllamaConfig _ollamaConfig;
        private readonly int _chunkSize;

        /// <summary>
        /// 初始化OllamaRAGService
        /// </summary>
        /// <param name="storageFolder">存储文件夹路径</param>
        /// <param name="ollamaConfig">Ollama配置</param>
        /// <param name="chunkSize">分块大小</param>
        public OllamaRAGService(string storageFolder, OllamaConfig ollamaConfig = null, int chunkSize = 200)
            : base(storageFolder)
        {
            _ollamaConfig = ollamaConfig ?? CreateDefaultOllamaConfig();
            _chunkSize = chunkSize;

            // 初始化内存实例
            _memory = CreateMemory();
        }

        /// <summary>
        /// 创建默认的Ollama配置
        /// </summary>
        private static OllamaConfig CreateDefaultOllamaConfig()
        {
            return new OllamaConfig
            {
                EmbeddingModel = new OllamaModelConfig("bge-m3") { MaxTokenTotal = 2048 },
                Endpoint = "http://localhost:11434/"
            };
        }

        /// <inheritdoc/>
        protected override IKernelMemory CreateMemory()
        {
            // 配置文本分块
            var textPartitioningOptions = new TextPartitioningOptions
            {
                MaxTokensPerParagraph = _chunkSize,
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
            return new KernelMemoryBuilder()
                .WithOllamaTextEmbeddingGeneration(_ollamaConfig)
                .WithCustomTextPartitioningOptions(textPartitioningOptions)
                .WithSimpleFileStorage(storageConfig)
                .WithSimpleVectorDb(vectorDbConfig)
                .WithSearchClientConfig(searchClientConfig)
                .WithoutTextGenerator()
                .Build();
        }

        /// <summary>
        /// 获取Ollama配置
        /// </summary>
        public OllamaConfig OllamaConfig => _ollamaConfig;

        /// <summary>
        /// 获取分块大小
        /// </summary>
        public int ChunkSize => _chunkSize;
    }
}