using Microsoft.Extensions.Logging;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.AI;
using Microsoft.KernelMemory.Context;
using Microsoft.KernelMemory.Diagnostics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

#pragma warning disable KMEXP00 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

namespace AI.KnowledgeBase.Tencent
{
    public class TencentTextEmbeddingGenerator : ITextEmbeddingGenerator, ITextEmbeddingBatchGenerator
    {
        private readonly GetEmbedding _embeddingService;
        private readonly ITextTokenizer _textTokenizer;
        private readonly IContextProvider _contextProvider;
        private readonly ILogger<TencentTextEmbeddingGenerator> _log;

        // 腾讯云文本嵌入模型的最大token数
        public int MaxTokens { get; }

        // 腾讯云批量处理的最大数量（根据GetEmbedding.cs中的限制）
        public int MaxBatchSize { get; }

        public TencentTextEmbeddingGenerator(
            GetEmbedding embeddingService,
            ITextTokenizer? textTokenizer = null,
            IContextProvider? contextProvider = null,
            ILoggerFactory? loggerFactory = null)
        {
            this._embeddingService = embeddingService ?? throw new ArgumentNullException(nameof(embeddingService));
            this._log = (loggerFactory ?? DefaultLogger.Factory).CreateLogger<TencentTextEmbeddingGenerator>();

            // 使用默认的CL100K分词器
            textTokenizer ??= new CL100KTokenizer();
            this._textTokenizer = textTokenizer;

            this._contextProvider = contextProvider ?? new RequestContextProvider();

            // 腾讯云文本嵌入模型的最大token数（根据文档设置）
            this.MaxTokens = 2000; // 可以根据实际模型调整

            // 腾讯云批量处理的最大数量为7
            this.MaxBatchSize = 7;
        }

        /// <summary>
        /// 从环境变量创建TencentTextEmbeddingGenerator实例
        /// </summary>
        public TencentTextEmbeddingGenerator(
            string region = "ap-guangzhou",
            ITextTokenizer? textTokenizer = null,
            IContextProvider? contextProvider = null,
            ILoggerFactory? loggerFactory = null)
            : this(GetEmbedding.CreateFromConfiguration(null, region), textTokenizer, contextProvider, loggerFactory)
        {
        }

        /// <summary>
        /// 使用指定的SecretId和SecretKey创建TencentTextEmbeddingGenerator实例
        /// </summary>
        public TencentTextEmbeddingGenerator(
            string secretId,
            string secretKey,
            string region = "ap-guangzhou",
            ITextTokenizer? textTokenizer = null,
            IContextProvider? contextProvider = null,
            ILoggerFactory? loggerFactory = null)
            : this(new GetEmbedding(secretId, secretKey, region), textTokenizer, contextProvider, loggerFactory)
        {
        }

        public int CountTokens(string text)
        {
            return this._textTokenizer.CountTokens(text);
        }

        public IReadOnlyList<string> GetTokens(string text)
        {
            return this._textTokenizer.GetTokens(text);
        }

        public async Task<Embedding> GenerateEmbeddingAsync(
            string text,
            CancellationToken cancellationToken = default)
        {
            this._log.LogTrace("Generating embedding, text length {0} chars", text.Length);

            // 检查token数量是否超过限制
            int tokenCount = CountTokens(text);
            if (tokenCount > this.MaxTokens)
            {
                throw new ArgumentException($"文本token数量({tokenCount})超过最大限制({this.MaxTokens})");
            }

            // 使用query类型生成嵌入
            var response = await this._embeddingService.GetEmbeddingAsync(
                text, 
                GetEmbedding.DefaultModel, 
                GetEmbedding.TextTypeQuery,
                cancellationToken: cancellationToken);

            if (response.Data == null || response.Data.Length == 0 || response.Data[0] == null)
            {
                throw new InvalidOperationException("腾讯云嵌入服务返回空响应");
            }

            var embeddingData = response.Data[0].Embedding;
            if (embeddingData == null)
            {
                throw new InvalidOperationException("腾讯云嵌入服务返回空向量");
            }

            // 将float?数组转换为float数组
            var embeddingArray = embeddingData.Select(x => x ?? 0f).ToArray();
            var embedding = new Embedding(embeddingArray);

            this._log.LogTrace("Embedding ready, vector length {0}", embedding.Length);

            return embedding;
        }

        public async Task<Embedding[]> GenerateEmbeddingBatchAsync(
            IEnumerable<string> textList,
            CancellationToken cancellationToken = default)
        {
            var list = textList.ToList();

            if (list.Count == 0)
            {
                return Array.Empty<Embedding>();
            }

            if (list.Count > this.MaxBatchSize)
            {
                throw new ArgumentException($"批量文本数量({list.Count})超过最大限制({this.MaxBatchSize})");
            }

            this._log.LogTrace("Generating embeddings batch, size {0} texts", list.Count);

            // 检查每个文本的token数量
            foreach (var text in list)
            {
                int tokenCount = CountTokens(text);
                if (tokenCount > this.MaxTokens)
                {
                    throw new ArgumentException($"文本token数量({tokenCount})超过最大限制({this.MaxTokens})");
                }
            }

            // 使用query类型生成批量嵌入
            var response = await this._embeddingService.GetEmbeddingsAsync(
                list, 
                GetEmbedding.DefaultModel, 
                GetEmbedding.TextTypeQuery,
                cancellationToken: cancellationToken);

            if (response.Data == null)
            {
                throw new InvalidOperationException("腾讯云嵌入服务返回空响应");
            }

            var result = new List<Embedding>();
            foreach (var embeddingObj in response.Data)
            {
                if (embeddingObj == null || embeddingObj.Embedding == null)
                {
                    result.Add(new Embedding(Array.Empty<float>()));
                }
                else
                {
                    // 将float?数组转换为float数组
                    var embeddingArray = embeddingObj.Embedding.Select(x => x ?? 0f).ToArray();
                    result.Add(new Embedding(embeddingArray));
                }
            }

            this._log.LogTrace("Embeddings batch ready, size {0} texts", result.Count);

            return result.ToArray();
        }
    }

}

#pragma warning restore KMEXP00 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
