using AI.KnowledgeBase.MemoryStorage;
using AI.KnowledgeBase.MemoryStorage.BM25;
using Microsoft.Extensions.Configuration;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.Configuration;
using Microsoft.KernelMemory.DocumentStorage.DevTools;
using Microsoft.KernelMemory.FileSystem.DevTools;
using Microsoft.KernelMemory.Handlers;
using Microsoft.KernelMemory.MemoryStorage.DevTools;
using Microsoft.KernelMemory.Pipeline;
using Microsoft.SemanticKernel.Memory;
using NetTopologySuite.Utilities;
using System;

namespace AI.KnowledgeBase.Services
{
    /// <summary>
    /// 基于腾讯云的RAG服务实现
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2007:Consider calling ConfigureAwait on the awaited task", Justification = "<Pending>")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA1303:Do not pass literals as localized parameters", Justification = "<Pending>")]
    public class TencentRAGService : RAGServiceBase
    {
        private readonly TencentConfig _tencentConfig;
        private readonly int _chunkSize;
        private readonly IConfiguration _configuration;

        /// <summary>
        /// 初始化TencentRAGService
        /// </summary>
        public TencentRAGService(string storageFolder, IConfiguration configuration = null, int chunkSize = 400)
            : base(storageFolder)
        {
            _configuration = configuration;
            _chunkSize = chunkSize;

            // 从配置中获取腾讯云配置
            _tencentConfig = CreateTencentConfigFromConfiguration();

            // 初始化内存实例
            _memory = CreateMemory();
        }

        /// <summary>
        /// 从配置创建腾讯云配置
        /// </summary>
        private TencentConfig CreateTencentConfigFromConfiguration()
        {
            if (_configuration != null)
            {
                return new TencentConfig
                {
                    SecretId = _configuration["TencentCloud:SecretId"] ??
                              Environment.GetEnvironmentVariable("TENCENTCLOUD_SECRET_ID") ??
                              string.Empty,
                    SecretKey = _configuration["TencentCloud:SecretKey"] ??
                               Environment.GetEnvironmentVariable("TENCENTCLOUD_SECRET_KEY") ??
                               string.Empty,
                    Region = _configuration["TencentCloud:Region"] ??
                            Environment.GetEnvironmentVariable("TENCENTCLOUD_REGION") ??
                            "ap-guangzhou",
                    EmbeddingModel = _configuration["TencentCloud:EmbeddingModel"] ??
                                    "text-embedding-v1"
                };
            }
            else
            {
                // 回退到环境变量
                return new TencentConfig
                {
                    SecretId = Environment.GetEnvironmentVariable("TENCENTCLOUD_SECRET_ID") ?? string.Empty,
                    SecretKey = Environment.GetEnvironmentVariable("TENCENTCLOUD_SECRET_KEY") ?? string.Empty,
                    Region = Environment.GetEnvironmentVariable("TENCENTCLOUD_REGION") ?? "ap-guangzhou",
                    EmbeddingModel = "text-embedding-v1"
                };
            }
        }

        /// <inheritdoc/>
        protected override IKernelMemory CreateMemory()
        {
            // 配置文本分块
            var textPartitioningOptions = new TextPartitioningOptions
            {
                MaxTokensPerParagraph = _chunkSize,
                //OverlappingTokens = 40,
                OverlappingTokens = Math.Max(10, _chunkSize * 2 / 10),  // 20%的重叠，至少10个标记
            };

            // 配置存储
            var storageConfig = new SimpleFileStorageConfig
            {
                Directory = _storageFolder,
                StorageType = FileSystemTypes.Disk,
            };

            // 配置文本数据库
            var textDbConfig = new SimpleTextDbConfig
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

            // 配置文本数据库
            var bm25DbConfig = new BM25MemoryDbConfig
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
            var builder = new KernelMemoryBuilder()
                .WithCustomTextPartitioningOptions(textPartitioningOptions)
                .WithSimpleFileStorage(storageConfig)
                //.WithSimpleTextDb(textDbConfig)
                //.WithSimpleVectorDb(vectorDbConfig)
                .WithSimpleBM25DbDb(bm25DbConfig)
                .WithSearchClientConfig(searchClientConfig)
                .WithoutTextGenerator();

            // 添加腾讯云文本嵌入生成器
            if (!string.IsNullOrEmpty(_tencentConfig.SecretId) && !string.IsNullOrEmpty(_tencentConfig.SecretKey))
            {
                // 使用TencentTextEmbeddingGenerator
                var embeddingGenerator = new AI.KnowledgeBase.Tencent.TencentTextEmbeddingGenerator(
                    _tencentConfig.SecretId,
                    _tencentConfig.SecretKey,
                    _tencentConfig.Region);
                
                builder.WithCustomEmbeddingGenerator(embeddingGenerator);
            }
            else
            {
                throw new InvalidOperationException("腾讯云SecretId和SecretKey未配置。请设置环境变量TENCENT_SECRET_ID和TENCENT_SECRET_KEY，或在TencentConfig中提供。");
            }

            builder.WithoutDefaultHandlers();
            var memory = builder.Build<MemoryServerless>();

            memory.Orchestrator.AddDefaultHandlers2();

            return memory;
        }

        /// <summary>
        /// 获取腾讯云配置
        /// </summary>
        public TencentConfig TencentConfig => _tencentConfig;

        /// <summary>
        /// 获取分块大小
        /// </summary>
        public int ChunkSize => _chunkSize;
    }

    /// <summary>
    /// 腾讯云配置类
    /// </summary>
    public class TencentConfig
    {
        /// <summary>
        /// 腾讯云SecretId
        /// </summary>
        public string SecretId { get; set; }

        /// <summary>
        /// 腾讯云SecretKey
        /// </summary>
        public string SecretKey { get; set; }

        /// <summary>
        /// 区域（如：ap-guangzhou）
        /// </summary>
        public string Region { get; set; } = "ap-guangzhou";

        /// <summary>
        /// 嵌入模型名称
        /// </summary>
        public string EmbeddingModel { get; set; } = "text-embedding-v1";
    }
}
