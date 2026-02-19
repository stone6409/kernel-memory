using Microsoft.KernelMemory;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace AI.KnowledgeBase.Services
{
    /// <summary>
    /// RAG（检索增强生成）服务接口
    /// </summary>
    public interface IRAGService : IDisposable
    {
        /// <summary>
        /// 导入文档到内存
        /// </summary>
        /// <param name="filePath">文档路径</param>
        /// <param name="documentId">文档ID</param>
        /// <param name="index">索引名称（可选）</param>
        /// <returns>文档ID</returns>
        Task<string> ImportDocumentAsync(string filePath, string documentId, string? index = null);

        /// <summary>
        /// 导入文本内容到内存
        /// </summary>
        /// <param name="content">文本内容</param>
        /// <param name="documentId">文档ID</param>
        /// <param name="index">索引名称（可选）</param>
        /// <returns>文档ID</returns>
        Task<string> ImportTextAsync(string content, string documentId, string? index = null);

        /// <summary>
        /// 搜索相关文档
        /// </summary>
        /// <param name="query">查询内容</param>
        /// <param name="index">索引名称（可选）</param>
        /// <param name="minRelevance">最小相关性</param>
        /// <param name="limit">返回结果数量</param>
        /// <returns>搜索结果</returns>
        Task<SearchResult> SearchAsync(string query, string? index = null, float minRelevance = 0.4f, int limit = 10);

        /// <summary>
        /// 获取答案
        /// </summary>
        /// <param name="question">问题</param>
        /// <param name="index">索引名称（可选）</param>
        /// <param name="minRelevance">最小相关性</param>
        /// <returns>答案结果</returns>
        Task<MemoryAnswer> AskAsync(string question, string? index = null, float minRelevance = 0.4f);

        /// <summary>
        /// 导入指定文件夹下的所有文件
        /// </summary>
        /// <param name="folderPath">文件夹路径</param>
        /// <param name="includePatterns">包含文件的模式集合</param>
        /// <param name="excludePaths">排除文件的路径集合</param>
        /// <param name="excludeFolders">排除的文件夹名称集合</param>
        /// <param name="index">索引名称（可选）</param>
        /// <returns>导入的文件数量</returns>
        Task<int> ImportDocumentsFromFolderAsync(
            string folderPath,
            IEnumerable<string> includePatterns,
            IEnumerable<string> excludePaths = null,
            IEnumerable<string> excludeFolders = null,
            string? index = null);

        /// <summary>
        /// 获取所有索引的名称
        /// </summary>
        /// <returns>索引名称列表</returns>
        Task<IEnumerable<string>> ListIndexesAsync();

        /// <summary>
        /// 检查索引是否存在
        /// </summary>
        /// <param name="indexName">索引名称</param>
        /// <returns>是否存在</returns>
        Task<bool> IndexExistsAsync(string indexName);

        /// <summary>
        /// 删除索引
        /// </summary>
        /// <param name="indexName">索引名称</param>
        /// <returns>是否成功</returns>
        Task<bool> DeleteIndexAsync(string indexName);

        /// <summary>
        /// 删除文档
        /// </summary>
        /// <param name="documentId">文档ID</param>
        /// <param name="index">索引名称（可选）</param>
        /// <returns>是否成功</returns>
        Task<bool> DeleteDocumentAsync(string documentId, string? index = null);

        /// <summary>
        /// 获取文档信息
        /// </summary>
        /// <param name="documentId">文档ID</param>
        /// <param name="index">索引名称（可选）</param>
        /// <returns>文档信息</returns>
        Task<DataPipelineStatus?> GetDocumentStatusAsync(string documentId, string? index = null);

        /// <summary>
        /// 打印搜索结果
        /// </summary>
        /// <param name="result">搜索结果</param>
        void PrintSearchResult(SearchResult result);

        /// <summary>
        /// 打印答案结果
        /// </summary>
        /// <param name="answer">答案结果</param>
        void PrintAnswerResult(MemoryAnswer answer);
    }
}