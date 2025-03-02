// Copyright (c) Microsoft. All rights reserved.

using Microsoft.KernelMemory.AI.Ollama;
using Microsoft.KernelMemory.Configuration;
using Microsoft.KernelMemory;
using System.Collections.ObjectModel;
using static Microsoft.KernelMemory.OpenAIConfig;
using Microsoft.KernelMemory.AI;
using Microsoft.KernelMemory.Context;
using static Microsoft.KernelMemory.Constants;

namespace SimpleRAGWithOllama;

#pragma warning disable CA1303
internal class Program
{
    static async Task Main(string[] args)
    {
        var ollamaConfig = new OllamaConfig()
        {
            TextModel = new OllamaModelConfig("deepseek-r1:1.5b") { MaxTokenTotal = 125000, Seed = 42, TopK = 7 },
            EmbeddingModel = new OllamaModelConfig("bge-m3") { MaxTokenTotal = 2048 },
            Endpoint = "http://localhost:11434/"
        };

        //var openAIConfig = new OpenAIConfig
        //{
        //    Endpoint = "https://api.moonshot.cn/v1/",
        //    TextModel = "moonshot-v1-8k",
        //    APIKey = "sk-UmawlPaRLJ2xjl31lUZfwJTUelTshEyPYPQr4NAtpeXuHffg"
        //};

        //var openAIConfig = new OpenAIConfig
        //{
        //    //TextGenerationType = TextGenerationTypes.Chat,
        //    Endpoint = "https://ark.cn-beijing.volces.com/api/v3/",
        //    TextModel = "ep-20250220113714-zcj7m", // "DeepSeek-V3"
        //    APIKey = "2b892e90-e044-43fe-9293-a1783f1e0aeb"
        //};

        var openAIConfig = new OpenAIConfig
        {
            //TextGenerationType = TextGenerationTypes.Chat,
            Endpoint = "https://api.lkeap.cloud.tencent.com/v1/",
            TextModel = "deepseek-v3",
            APIKey = "sk-L7hpFlDbWTfRYVVTFHVsukGrS11BzRExwjYcTZCzeHs0AQyi",
            //TextModelMaxTokenTotal = 4096,
        };

        //var textGenerationOptions = new TextGenerationOptions
        //{
        //    MaxTokens = 4096,
        //    Temperature = 0.7d,
        //};

        var memoryBuilder = new KernelMemoryBuilder()
            //.WithOllamaTextGeneration(ollamaConfig)
            .WithOpenAITextGeneration(openAIConfig)
            .WithOllamaTextEmbeddingGeneration(ollamaConfig)
            .WithSearchClientConfig(new SearchClientConfig()
            {
                AnswerTokens = 4096
            })
            .WithCustomTextPartitioningOptions(new TextPartitioningOptions()
            {
                //MaxTokensPerLine = 50,
                MaxTokensPerParagraph = 200,
                OverlappingTokens = 50
            });

        var memory = memoryBuilder.Build();

        var index = "ragwithollama";

        var indexes = await memory.ListIndexesAsync().ConfigureAwait(false);
        if (indexes.Any(i => String.Equals(i.Name, index, StringComparison.OrdinalIgnoreCase)))
        {
            await memory.DeleteIndexAsync(index).ConfigureAwait(false);
        }

        var document = new Document().AddFiles(["Data/Persons.txt"]);

        var documentID = await memory.ImportDocumentAsync(document, index: index).ConfigureAwait(false);

        var memoryFilter = MemoryFilters.ByDocument(documentID);

        var context = new RequestContext();
        context.SetArg(CustomContext.Rag.MaxTokens, 4096);
        context.SetArg(CustomContext.Rag.Temperature, 0.7d);
        context.SetArg(CustomContext.Rag.NucleusSampling, 1d);

        var chatHistory = new ChatHistory();
        Console.WriteLine("You can exit the console by tapping 'Exit'.");
        Console.WriteLine("第一个问题: 伊桑·卡特是什么时候出生的？");
        var userInput = "伊桑·卡特是什么时候出生的？";

        SearchResult answerResult = await memory.SearchAsync(userInput,
                index,
                memoryFilter,
                minRelevance: .6f).ConfigureAwait(false);

        while (userInput != "Exit")
        {
            if (string.IsNullOrWhiteSpace(userInput))
            {
                continue;
            }

            var conversationContext = chatHistory.GetHistoryAsContext();
            var fullQuery = ComposeQuery(userInput, conversationContext);
            var answer = await memory.AskAsync(
                fullQuery,
                index,
                memoryFilter,
                context: context,
                minRelevance: .6f).ConfigureAwait(false);

            chatHistory.AddUserMessage(userInput);
            chatHistory.AddAssistantMessage(answer.Result);
            Console.WriteLine(answer.Result);

            Console.WriteLine("Please Ask your question");
            userInput = Console.ReadLine();
        }
    }

    private static string ComposeQuery(string userInput, string conversationContext)
    {
        return $"{conversationContext}\nUser: {userInput}";
    }

    public class ChatHistory
    {
        private readonly Collection<string> _messages = [];

        public void AddUserMessage(string message)
        {
            this._messages.Add($"User: {message}");
        }

        public void AddAssistantMessage(string message)
        {
            this._messages.Add($"Assistant: {message}");
        }

        public string GetHistoryAsContext(int maxMessages = 10)
        {
            var recentMessages = this._messages.TakeLast(maxMessages);
            return string.Join("\n", recentMessages);
        }
    }
}
