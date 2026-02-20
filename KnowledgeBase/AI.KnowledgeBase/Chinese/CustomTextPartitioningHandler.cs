// 修改后的TextPartitioningHandler

using System.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.KernelMemory.Chunkers;
using Microsoft.KernelMemory.Configuration;
using Microsoft.KernelMemory.Diagnostics;
using Microsoft.KernelMemory.Pipeline;

namespace AI.KnowledgeBase.Chinese
{
    public sealed class CustomTextPartitioningHandler : IPipelineStepHandler
    {
        private readonly IPipelineOrchestrator _orchestrator;
        private readonly TextPartitioningOptions _options;
        private readonly ILogger<CustomTextPartitioningHandler> _log;
        private readonly int _maxTokensPerPartition = int.MaxValue;
        private readonly ChineseOptimizedChunker _chineseChunker;
        private readonly MarkDownChunker _markDownChunker;

        /// <inheritdoc />
        public string StepName { get; }

        public CustomTextPartitioningHandler(
            string stepName,
            IPipelineOrchestrator orchestrator,
            TextPartitioningOptions? options = null,
            ILoggerFactory? loggerFactory = null)
        {
            this.StepName = stepName;
            this._orchestrator = orchestrator;

            // 使用自定义的中文分块器
            this._chineseChunker = new ChineseOptimizedChunker(new ChineseTokenizer());
            this._markDownChunker = new MarkDownChunker(new ChineseTokenizer());

            this._options = options ?? new TextPartitioningOptions();
            this._options.Validate();

            this._log = (loggerFactory ?? DefaultLogger.Factory).CreateLogger<CustomTextPartitioningHandler>();
            this._log.LogInformation("Custom Chinese partitioning handler '{0}' ready", stepName);

            if (orchestrator.EmbeddingGenerationEnabled)
            {
                // 计算最大token数
                foreach (var gen in orchestrator.GetEmbeddingGenerators())
                {
                    this._maxTokensPerPartition = Math.Min(gen.MaxTokens, this._maxTokensPerPartition);
                }

                if (this._options.MaxTokensPerParagraph > this._maxTokensPerPartition)
                {
                    throw ChunkTooBigForEmbeddingsException(this._options.MaxTokensPerParagraph, this._maxTokensPerPartition, this._log);
                }
            }
        }

        /// <inheritdoc />
        public async Task<(ReturnType returnType, DataPipeline updatedPipeline)> InvokeAsync(
            DataPipeline pipeline, CancellationToken cancellationToken = default)
        {
            this._log.LogDebug("Partitioning text with custom Chinese chunker, pipeline '{0}/{1}'", pipeline.Index, pipeline.DocumentId);

            if (pipeline.Files.Count == 0)
            {
                this._log.LogWarning("Pipeline '{0}/{1}': there are no files to process, moving to next pipeline step.", pipeline.Index, pipeline.DocumentId);
                return (ReturnType.Success, pipeline);
            }

            var context = pipeline.GetContext();

            // 允许通过上下文参数覆盖段落大小
            var maxTokensPerChunk = context.GetCustomPartitioningMaxTokensPerChunkOrDefault(this._options.MaxTokensPerParagraph);
            if (maxTokensPerChunk > this._maxTokensPerPartition)
            {
                throw ChunkTooBigForEmbeddingsException(maxTokensPerChunk, this._maxTokensPerPartition, this._log);
            }

            // 允许通过上下文参数覆盖重叠token数
            var overlappingTokens = Math.Max(0, context.GetCustomPartitioningOverlappingTokensOrDefault(this._options.OverlappingTokens));

            string? chunkHeader = context.GetCustomPartitioningChunkHeaderOrDefault(null);

            foreach (DataPipeline.FileDetails uploadedFile in pipeline.Files)
            {
                // 跟踪新生成的文件
                Dictionary<string, DataPipeline.GeneratedFileDetails> newFiles = [];

                foreach (KeyValuePair<string, DataPipeline.GeneratedFileDetails> generatedFile in uploadedFile.GeneratedFiles)
                {
                    var file = generatedFile.Value;
                    if (file.AlreadyProcessedBy(this))
                    {
                        this._log.LogTrace("File {0} already processed by this handler", file.Name);
                        continue;
                    }

                    // 只分区原始文本
                    if (file.ArtifactType != DataPipeline.ArtifactTypes.ExtractedText)
                    {
                        this._log.LogTrace("Skipping file {0} (not original text)", file.Name);
                        continue;
                    }

                    // 根据文件类型使用不同的分区策略
                    List<string> chunks;
                    BinaryData fileContent = await this._orchestrator.ReadFileAsync(pipeline, file.Name, cancellationToken).ConfigureAwait(false);
                    string chunksMimeType = MimeTypes.PlainText;

                    // 跳过空分区
                    if (fileContent.IsEmpty) { continue; }

                    switch (file.MimeType)
                    {
                        case MimeTypes.PlainText:
                        {
                            this._log.LogDebug("Partitioning text file {0} with Chinese chunker", file.Name);
                            string content = fileContent.ToString();

                            // 使用自定义的中文分块器
                            chunks = this._chineseChunker.Split(content, new PlainTextChunkerOptions 
                            { 
                                MaxTokensPerChunk = maxTokensPerChunk, 
                                Overlap = overlappingTokens, 
                                ChunkHeader = chunkHeader 
                            });
                            break;
                        }

                        case MimeTypes.MarkDown:
                        {
                            this._log.LogDebug("Partitioning MarkDown file {0}", file.Name);
                            string content = fileContent.ToString();
                            chunksMimeType = MimeTypes.MarkDown;

                            // 使用自定义的Markdown分块器
                            chunks = this._markDownChunker.Split(content, new MarkDownChunkerOptions 
                            { 
                                MaxTokensPerChunk = maxTokensPerChunk, 
                                Overlap = overlappingTokens, 
                                ChunkHeader = chunkHeader 
                            });
                            break;
                        }

                        default:
                            this._log.LogWarning("File {0} cannot be partitioned, type '{1}' not supported", file.Name, file.MimeType);
                            // 不分区其他文件
                            continue;
                    }

                    if (chunks.Count == 0) { continue; }

                    this._log.LogDebug("Saving {0} file partitions", chunks.Count);
                    for (int partitionNumber = 0; partitionNumber < chunks.Count; partitionNumber++)
                    {
                        string text = chunks[partitionNumber];
                        int sectionNumber = 0;
                        BinaryData textData = new(text);

                        var destFile = uploadedFile.GetPartitionFileName(partitionNumber);
                        await this._orchestrator.WriteFileAsync(pipeline, destFile, textData, cancellationToken).ConfigureAwait(false);

                        var destFileDetails = new DataPipeline.GeneratedFileDetails
                        {
                            Id = Guid.NewGuid().ToString("N"),
                            ParentId = uploadedFile.Id,
                            Name = destFile,
                            Size = text.Length,
                            MimeType = chunksMimeType,
                            ArtifactType = DataPipeline.ArtifactTypes.TextPartition,
                            PartitionNumber = partitionNumber,
                            SectionNumber = sectionNumber,
                            Tags = pipeline.Tags,
                            ContentSHA256 = textData.CalculateSHA256(),
                        };
                        newFiles.Add(destFile, destFileDetails);
                        destFileDetails.MarkProcessedBy(this);
                    }

                    file.MarkProcessedBy(this);
                }

                // 将新文件添加到管道状态
                foreach (var file in newFiles)
                {
                    uploadedFile.GeneratedFiles.Add(file.Key, file.Value);
                }
            }

            return (ReturnType.Success, pipeline);
        }

        private static ConfigurationException ChunkTooBigForEmbeddingsException(int value, int limit, ILogger logger)
        {
            var errMsg = $"The configured partition size ({value} tokens) is too big for one " +
                         $"of the embedding generators in use. The max value allowed is {limit} tokens. " +
                         $"Consider changing the partitioning options, see {InternalConstants.DocsBaseUrl}/how-to/custom-partitioning for details.";
            logger.LogError(errMsg);
            return new ConfigurationException(errMsg);
        }
    }
}
