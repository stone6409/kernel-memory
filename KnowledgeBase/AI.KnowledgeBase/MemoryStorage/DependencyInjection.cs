// Copyright (c) Microsoft. All rights reserved.

using AI.KnowledgeBase.MemoryStorage.Keyword;
using AI.KnowledgeBase.MemoryStorage.Semantic;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.FileSystem.DevTools;
using Microsoft.KernelMemory.MemoryStorage;

// ReSharper disable once CheckNamespace - reduce number of "using" statements
namespace AI.KnowledgeBase.MemoryStorage;

/// <summary>
/// Kernel Memory builder extensions
/// </summary>
public static partial class KernelMemoryBuilderExtensions
{
    public static IKernelMemoryBuilder WithKeywordMemoryDb(this IKernelMemoryBuilder builder, KeywordMemoryDbConfig? config = null)
    {
        builder.Services.AddKeywordMemoryDbAsMemoryDb(config ?? new KeywordMemoryDbConfig());
        return builder;
    }

    public static IKernelMemoryBuilder WithKeywordMemoryDb(this IKernelMemoryBuilder builder, string directory)
    {
        builder.Services.AddKeywordMemoryDbAsMemoryDb(directory);
        return builder;
    }

    public static IKernelMemoryBuilder WithSemanticMemoryDb(this IKernelMemoryBuilder builder, SemanticMemoryDbConfig? config = null)
    {
        builder.Services.AddSemanticMemoryDbAsMemoryDb(config ?? new SemanticMemoryDbConfig());
        return builder;
    }

    public static IKernelMemoryBuilder WithSemanticMemoryDb(this IKernelMemoryBuilder builder, string directory)
    {
        builder.Services.AddSemanticMemoryDbAsMemoryDb(directory);
        return builder;
    }

}

/// <summary>
/// .NET IServiceCollection dependency injection extensions.
/// </summary>
public static partial class DependencyInjection
{
    public static IServiceCollection AddKeywordMemoryDbAsMemoryDb(this IServiceCollection services, KeywordMemoryDbConfig? config = null)
    {
        return services
            .AddSingleton<KeywordMemoryDbConfig>(config ?? new KeywordMemoryDbConfig())
            .AddSingleton<IMemoryDb, KeywordMemoryDb>();
    }

    public static IServiceCollection AddKeywordMemoryDbAsMemoryDb(this IServiceCollection services, string directory)
    {
        var config = new KeywordMemoryDbConfig { StorageType = FileSystemTypes.Disk, Directory = directory };
        return services.AddKeywordMemoryDbAsMemoryDb(config);
    }

    public static IServiceCollection AddSemanticMemoryDbAsMemoryDb(this IServiceCollection services, SemanticMemoryDbConfig? config = null)
    {
        return services
            .AddSingleton<SemanticMemoryDbConfig>(config ?? new SemanticMemoryDbConfig())
            .AddSingleton<IMemoryDb, SemanticMemoryDb>();
    }

    public static IServiceCollection AddSemanticMemoryDbAsMemoryDb(this IServiceCollection services, string directory)
    {
        var config = new SemanticMemoryDbConfig { StorageType = FileSystemTypes.Disk, Directory = directory };
        return services.AddSemanticMemoryDbAsMemoryDb(config);
    }

}
