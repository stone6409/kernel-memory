using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TencentCloud.Common.Profile;
using TencentCloud.Common;
using TencentCloud.Lkeap.V20240522;
using TencentCloud.Lkeap.V20240522.Models;
using Microsoft.Extensions.Configuration;

namespace AI.KnowledgeBase.Tencent
{
    /// <summary>
    /// 腾讯云向量嵌入服务封装类
    /// </summary>
    public class GetEmbedding
    {
        private readonly LkeapClient _client;
        private readonly Credential _credential;
        private readonly string _region;

        // 模型常量定义
        public const string DefaultModel = "lke-text-embedding-v2";
        public const string ModelV1 = "lke-text-embedding-v1";

        // 文本类型常量
        public const string TextTypeDocument = "document";
        public const string TextTypeQuery = "query";

        /// <summary>
        /// 初始化GetEmbedding实例
        /// </summary>
        /// <param name="secretId">腾讯云SecretId</param>
        /// <param name="secretKey">腾讯云SecretKey</param>
        /// <param name="region">区域，默认为ap-guangzhou</param>
        /// <param name="token">临时令牌（可选）</param>
        public GetEmbedding(string secretId, string secretKey, string region = "ap-guangzhou", string token = null)
        {
            if (string.IsNullOrEmpty(secretId))
                throw new ArgumentException("SecretId不能为空", nameof(secretId));
            if (string.IsNullOrEmpty(secretKey))
                throw new ArgumentException("SecretKey不能为空", nameof(secretKey));
            if (string.IsNullOrEmpty(region))
                throw new ArgumentException("区域不能为空", nameof(region));

            _credential = new Credential
            {
                SecretId = secretId,
                SecretKey = secretKey,
                Token = token
            };

            _region = region;

            // 创建客户端配置
            var clientProfile = new ClientProfile();
            var httpProfile = new HttpProfile
            {
                Endpoint = "lkeap.tencentcloudapi.com"
            };
            clientProfile.HttpProfile = httpProfile;

            // 创建客户端实例
            _client = new LkeapClient(_credential, _region, clientProfile);
        }

        /// <summary>
        /// 从配置创建GetEmbedding实例
        /// </summary>
        /// <param name="configuration">配置对象</param>
        /// <param name="region">区域，默认为ap-guangzhou</param>
        /// <returns>GetEmbedding实例</returns>
        public static GetEmbedding CreateFromConfiguration(IConfiguration configuration, string region = "ap-guangzhou")
        {
            var secretId = configuration["TencentCloud:SecretId"] ??
                          Environment.GetEnvironmentVariable("TENCENTCLOUD_SECRET_ID");
            var secretKey = configuration["TencentCloud:SecretKey"] ??
                           Environment.GetEnvironmentVariable("TENCENTCLOUD_SECRET_KEY");

            if (string.IsNullOrEmpty(secretId) || string.IsNullOrEmpty(secretKey))
            {
                throw new InvalidOperationException(
                    "未找到腾讯云配置。请检查用户机密或环境变量TENCENTCLOUD_SECRET_ID和TENCENTCLOUD_SECRET_KEY。");
            }

            return new GetEmbedding(secretId, secretKey, region);
        }

        /// <summary>
        /// 获取单个文本的向量嵌入（同步）
        /// </summary>
        /// <param name="text">要嵌入的文本</param>
        /// <param name="model">模型名称</param>
        /// <param name="textType">文本类型（query/document）</param>
        /// <param name="instruction">自定义指令词</param>
        /// <returns>向量嵌入响应</returns>
        public GetEmbeddingResponse GetEmbeddingSync(string text, string model = DefaultModel, string textType = TextTypeDocument, string instruction = "")
        {
            if (string.IsNullOrEmpty(text))
                throw new ArgumentException("文本不能为空", nameof(text));

            var req = new GetEmbeddingRequest
            {
                Model = model,
                Inputs = new[] { text },  // 注意：这里使用 Inputs 数组
                TextType = textType,
                Instruction = instruction
            };

            return _client.GetEmbeddingSync(req);
        }

        /// <summary>
        /// 获取单个文本的向量嵌入（异步）
        /// </summary>
        /// <param name="text">要嵌入的文本</param>
        /// <param name="model">模型名称</param>
        /// <param name="textType">文本类型（query/document）</param>
        /// <param name="instruction">自定义指令词</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>向量嵌入响应任务</returns>
        public async Task<GetEmbeddingResponse> GetEmbeddingAsync(string text, string model = DefaultModel, string textType = TextTypeDocument, string instruction = "", CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(text))
                throw new ArgumentException("文本不能为空", nameof(text));

            var req = new GetEmbeddingRequest
            {
                Model = model,
                Inputs = new[] { text },  // 注意：这里使用 Inputs 数组
                TextType = textType,
                Instruction = instruction
            };

            // 检查是否已取消
            cancellationToken.ThrowIfCancellationRequested();

            return await _client.GetEmbedding(req);
        }

        /// <summary>
        /// 批量获取文本的向量嵌入（同步）
        /// </summary>
        /// <param name="texts">文本列表（最多7条）</param>
        /// <param name="model">模型名称</param>
        /// <param name="textType">文本类型（query/document）</param>
        /// <param name="instruction">自定义指令词</param>
        /// <returns>向量嵌入响应</returns>
        public GetEmbeddingResponse GetEmbeddingsSync(IEnumerable<string> texts, string model = DefaultModel, string textType = TextTypeDocument, string instruction = "")
        {
            if (texts == null)
                throw new ArgumentNullException(nameof(texts));

            var textList = new List<string>(texts);
            if (textList.Count == 0)
                throw new ArgumentException("文本列表不能为空", nameof(texts));

            if (textList.Count > 7)
                throw new ArgumentException("文本列表最多只能包含7条文本", nameof(texts));

            var req = new GetEmbeddingRequest
            {
                Model = model,
                Inputs = textList.ToArray(),
                TextType = textType,
                Instruction = instruction
            };

            return _client.GetEmbeddingSync(req);
        }

        /// <summary>
        /// 批量获取文本的向量嵌入（异步）
        /// </summary>
        /// <param name="texts">文本列表（最多7条）</param>
        /// <param name="model">模型名称</param>
        /// <param name="textType">文本类型（query/document）</param>
        /// <param name="instruction">自定义指令词</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>向量嵌入响应任务</returns>
        public async Task<GetEmbeddingResponse> GetEmbeddingsAsync(IEnumerable<string> texts, string model = DefaultModel, string textType = TextTypeDocument, string instruction = "", CancellationToken cancellationToken = default)
        {
            if (texts == null)
                throw new ArgumentNullException(nameof(texts));

            var textList = new List<string>(texts);
            if (textList.Count == 0)
                throw new ArgumentException("文本列表不能为空", nameof(texts));

            if (textList.Count > 7)
                throw new ArgumentException("文本列表最多只能包含7条文本", nameof(texts));

            var req = new GetEmbeddingRequest
            {
                Model = model,
                Inputs = textList.ToArray(),
                TextType = textType,
                Instruction = instruction
            };

            // 检查是否已取消
            cancellationToken.ThrowIfCancellationRequested();

            return await _client.GetEmbedding(req);
        }

        /// <summary>
        /// 获取单个文本的向量嵌入并返回JSON字符串
        /// </summary>
        /// <param name="text">要嵌入的文本</param>
        /// <param name="model">模型名称</param>
        /// <param name="textType">文本类型（query/document）</param>
        /// <param name="instruction">自定义指令词</param>
        /// <returns>JSON格式的向量嵌入响应</returns>
        public string GetEmbeddingAsJson(string text, string model = DefaultModel, string textType = TextTypeDocument, string instruction = "")
        {
            var response = GetEmbeddingSync(text, model, textType, instruction);
            return AbstractModel.ToJsonString(response);
        }

        /// <summary>
        /// 获取向量数组（简化访问）
        /// </summary>
        /// <param name="text">要嵌入的文本</param>
        /// <param name="model">模型名称</param>
        /// <param name="textType">文本类型（query/document）</param>
        /// <param name="instruction">自定义指令词</param>
        /// <returns>向量数组</returns>
        public float?[] GetEmbeddingVector(string text, string model = DefaultModel, string textType = TextTypeDocument, string instruction = "")
        {
            var response = GetEmbeddingSync(text, model, textType, instruction);
            
            if (response.Data == null || response.Data.Length == 0 || response.Data[0] == null)
                return Array.Empty<float?>();
                
            return response.Data[0].Embedding ?? Array.Empty<float?>();
        }

        /// <summary>
        /// 批量获取向量数组（简化访问）
        /// </summary>
        /// <param name="texts">文本列表</param>
        /// <param name="model">模型名称</param>
        /// <param name="textType">文本类型（query/document）</param>
        /// <param name="instruction">自定义指令词</param>
        /// <returns>向量数组列表</returns>
        public List<float?[]> GetEmbeddingVectors(IEnumerable<string> texts, string model = DefaultModel, string textType = TextTypeDocument, string instruction = "")
        {
            var response = GetEmbeddingsSync(texts, model, textType, instruction);
            var result = new List<float?[]>();
            
            if (response.Data != null)
            {
                foreach (var embeddingObj in response.Data)
                {
                    if (embeddingObj != null && embeddingObj.Embedding != null)
                    {
                        result.Add(embeddingObj.Embedding);
                    }
                    else
                    {
                        result.Add(Array.Empty<float?>());
                    }
                }
            }
            
            return result;
        }

        /// <summary>
        /// 获取客户端实例
        /// </summary>
        public LkeapClient Client => _client;

        /// <summary>
        /// 获取凭据信息
        /// </summary>
        public Credential Credential => _credential;

        /// <summary>
        /// 获取区域信息
        /// </summary>
        public string Region => _region;
    }
}