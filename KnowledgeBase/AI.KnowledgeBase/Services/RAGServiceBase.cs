using Microsoft.KernelMemory;
using Microsoft.KernelMemory.Pipeline;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace AI.KnowledgeBase.Services
{
    /// <summary>
    /// RAG（检索增强生成）服务抽象基类
    /// </summary>
    public abstract class RAGServiceBase : IRAGService
    {
        protected IKernelMemory _memory;
        protected readonly string _storageFolder;
        private bool _disposed = false;

        /// <summary>
        /// 初始化RAG服务基类
        /// </summary>
        /// <param name="storageFolder">存储文件夹路径</param>
        protected RAGServiceBase(string storageFolder)
        {
            _storageFolder = storageFolder ?? throw new ArgumentNullException(nameof(storageFolder));
            //_memory = CreateMemory(); // 在构造函数中创建内存实例
        }

        /// <summary>
        /// 获取KernelMemory实例（由子类实现）
        /// </summary>
        protected abstract IKernelMemory CreateMemory();


        /// <inheritdoc/>
        public virtual async Task<string> ImportDocumentAsync(string filePath, string documentId, string? index = null)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("Document file not found.", filePath);
            }

            Console.WriteLine($"Importing document: {Path.GetFileName(filePath)}");

            string docId = null;
            try
            {
                docId = await _memory.ImportDocumentAsync(filePath, documentId, null, index);
                Console.WriteLine($"- Document Id: {docId}");
            }
            catch (MimeTypeException)
            {
                string content = File.ReadAllText(filePath);
                docId = await _memory.ImportTextAsync(content, documentId, null, index);
                Console.WriteLine($"- Document Id: {docId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error importing document: {ex.Message}");
                throw;
            }

            return docId;
        }

        /// <inheritdoc/>
        public virtual async Task<string> ImportTextAsync(string content, string documentId, string? index = null)
        {
            if (string.IsNullOrEmpty(content))
            {
                throw new ArgumentException("Content cannot be null or empty", nameof(content));
            }

            Console.WriteLine($"Importing text content (length: {content.Length})");

            try
            {
                var docId = await _memory.ImportTextAsync(content, documentId, null, index);
                Console.WriteLine($"- Document Id: {docId}");
                return docId;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error importing text: {ex.Message}");
                throw;
            }
        }

        /// <inheritdoc/>
        public virtual async Task<SearchResult> SearchAsync(string query, string? index = null, float minRelevance = 0.2f, int limit = 10)
        {
            if (string.IsNullOrEmpty(query))
            {
                throw new ArgumentException("Query cannot be null or empty", nameof(query));
            }

            Console.WriteLine($"Searching for: {query}");

            try
            {
                var result = await _memory.SearchAsync(query, index: index, minRelevance: minRelevance, limit: limit);
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error searching: {ex.Message}");
                throw;
            }
        }

        /// <inheritdoc/>
        public virtual async Task<MemoryAnswer> AskAsync(string question, string? index = null, float minRelevance = 0.3f)
        {
            if (string.IsNullOrEmpty(question))
            {
                throw new ArgumentException("Question cannot be null or empty", nameof(question));
            }

            Console.WriteLine($"Asking: {question}");

            try
            {
                var answer = await _memory.AskAsync(question, index: index, minRelevance: minRelevance);
                return answer;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error asking question: {ex.Message}");
                throw;
            }
        }

        /// <inheritdoc/>
        public virtual async Task<int> ImportDocumentsFromFolderAsync(
            string folderPath,
            IEnumerable<string> includePatterns,
            IEnumerable<string> excludePaths = null,
            IEnumerable<string> excludeFolders = null,
            string? index = null)
        {
            if (!Directory.Exists(folderPath))
            {
                throw new DirectoryNotFoundException($"Folder not found: {folderPath}");
            }

            if (includePatterns == null || !includePatterns.Any())
            {
                throw new ArgumentException("Include patterns cannot be null or empty", nameof(includePatterns));
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

            // 如果存在排除文件夹集合，过滤掉排除的文件夹
            if (excludeFolders != null && excludeFolders.Any())
            {
                files = files
                    .Where(file => !excludeFolders.Any(exclude => file.Split(Path.DirectorySeparatorChar).Contains(exclude)))
                    .ToList();
            }

            // 逐个导入文件
            int importedCount = 0;
            foreach (var file in files)
            {
                try
                {
                    // 获取文件相对于 folderPath 的相对路径作为 documentId
                    var documentId = Path.GetRelativePath(folderPath, file).Replace("\\", "_");
                    await ImportDocumentAsync(file, documentId, index);
                    importedCount++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error importing file '{file}': {ex.Message}");
                }
            }

            return importedCount;
        }

        /// <inheritdoc/>
        public virtual async Task<IEnumerable<string>> ListIndexesAsync()
        {
            try
            {
                var indexes = await _memory.ListIndexesAsync();
                return indexes.Select(index => index.Name);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error listing indexes: {ex.Message}");
                throw;
            }
        }

        /// <inheritdoc/>
        public virtual async Task<bool> IndexExistsAsync(string indexName)
        {
            if (string.IsNullOrEmpty(indexName))
            {
                throw new ArgumentException("Index name cannot be null or empty", nameof(indexName));
            }

            try
            {
                var indexes = await ListIndexesAsync();
                return indexes.Contains(indexName);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error checking if index exists: {ex.Message}");
                throw;
            }
        }

        /// <inheritdoc/>
        public virtual async Task<bool> DeleteIndexAsync(string indexName)
        {
            if (string.IsNullOrEmpty(indexName))
            {
                throw new ArgumentException("Index name cannot be null or empty", nameof(indexName));
            }

            try
            {
                await _memory.DeleteIndexAsync(indexName);
                Console.WriteLine($"Index '{indexName}' deleted successfully");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting index '{indexName}': {ex.Message}");
                return false;
            }
        }

        /// <inheritdoc/>
        public virtual async Task<bool> DeleteDocumentAsync(string documentId, string? index = null)
        {
            if (string.IsNullOrEmpty(documentId))
            {
                throw new ArgumentException("Document ID cannot be null or empty", nameof(documentId));
            }

            try
            {
                await _memory.DeleteDocumentAsync(documentId, index);
                Console.WriteLine($"Document '{documentId}' deleted successfully");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting document '{documentId}': {ex.Message}");
                return false;
            }
        }

        /// <inheritdoc/>
        public virtual async Task<DataPipelineStatus?> GetDocumentStatusAsync(string documentId, string? index = null)
        {
            if (string.IsNullOrEmpty(documentId))
            {
                throw new ArgumentException("Document ID cannot be null or empty", nameof(documentId));
            }

            try
            {
                var status = await _memory.GetDocumentStatusAsync(documentId, index);
                return status;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting document status for '{documentId}': {ex.Message}");
                return null;
            }
        }

        /// <inheritdoc/>
        public virtual void PrintSearchResult(SearchResult result)
        {
            if (result == null)
            {
                Console.WriteLine("Search result is null");
                return;
            }

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

        /// <inheritdoc/>
        public virtual void PrintAnswerResult(MemoryAnswer answer)
        {
            if (answer == null)
            {
                Console.WriteLine("Answer is null");
                return;
            }

            Console.WriteLine($"Question: {answer.Question}");
            Console.WriteLine($"Answer: {answer.Result}");
            Console.WriteLine();

            if (answer.RelevantSources != null && answer.RelevantSources.Any())
            {
                Console.WriteLine("Relevant sources:");
                foreach (var source in answer.RelevantSources)
                {
                    Console.WriteLine($"- {source.SourceName} (relevance: {source.Partitions.FirstOrDefault()?.Relevance})");
                }
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        /// <param name="disposing">是否正在释放</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // 释放托管资源
                    if (_memory is IDisposable disposableMemory)
                    {
                        disposableMemory.Dispose();
                    }
                }

                _disposed = true;
            }
        }
    }
}
