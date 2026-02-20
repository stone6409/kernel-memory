using AI.KnowledgeBase.Chinese;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.Handlers;
using Microsoft.KernelMemory.Pipeline;

namespace AI.KnowledgeBase.Services
{
    public static partial class CustomDependencyInjection
    {
        // 关键方法：添加默认处理器
#pragma warning disable KMEXP04 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        public static InProcessPipelineOrchestrator AddDefaultHandlers2(this InProcessPipelineOrchestrator syncOrchestrator)
#pragma warning restore KMEXP04 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        {
            syncOrchestrator.AddHandler<TextExtractionHandler>(Constants.PipelineStepsExtract);
            syncOrchestrator.AddHandler<CustomTextPartitioningHandler>(Constants.PipelineStepsPartition);
            syncOrchestrator.AddHandler<SummarizationHandler>(Constants.PipelineStepsSummarize);
            syncOrchestrator.AddHandler<GenerateEmbeddingsHandler>(Constants.PipelineStepsGenEmbeddings);
            syncOrchestrator.AddHandler<SaveRecordsHandler>(Constants.PipelineStepsSaveRecords);
            syncOrchestrator.AddHandler<DeleteDocumentHandler>(Constants.PipelineStepsDeleteDocument);
            syncOrchestrator.AddHandler<DeleteIndexHandler>(Constants.PipelineStepsDeleteIndex);
            syncOrchestrator.AddHandler<DeleteGeneratedFilesHandler>(Constants.PipelineStepsDeleteGeneratedFiles);

            // Experimental handlers using parallelism
            syncOrchestrator.AddHandler<GenerateEmbeddingsParallelHandler>("gen_embeddings_parallel");
            syncOrchestrator.AddHandler<SummarizationParallelHandler>("summarize_parallel");

            return syncOrchestrator;
        }
   }
}
