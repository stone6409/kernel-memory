// Copyright (c) Microsoft. All rights reserved.

using AI.KnowledgeBase.MemoryStorage.BM25;
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
    public static IKernelMemoryBuilder WithSimpleBM25DbDb(this IKernelMemoryBuilder builder, BM25MemoryDbConfig? config = null)
    {
        builder.Services.AddSimpleBM25DbDbAsMemoryDb(config ?? new BM25MemoryDbConfig());
        return builder;
    }

    public static IKernelMemoryBuilder WithSimpleBM25DbDb(this IKernelMemoryBuilder builder, string directory)
    {
        builder.Services.AddSimpleBM25DbDbAsMemoryDb(directory);
        return builder;
    }
}

/// <summary>
/// .NET IServiceCollection dependency injection extensions.
/// </summary>
public static partial class DependencyInjection
{
    public static IServiceCollection AddSimpleBM25DbDbAsMemoryDb(this IServiceCollection services, BM25MemoryDbConfig? config = null)
    {
        return services
            .AddSingleton<BM25MemoryDbConfig>(config ?? new BM25MemoryDbConfig())
            .AddSingleton<IMemoryDb, BM25MemoryDb>();
    }

    public static IServiceCollection AddSimpleBM25DbDbAsMemoryDb(this IServiceCollection services, string directory)
    {
        var config = new BM25MemoryDbConfig { StorageType = FileSystemTypes.Disk, Directory = directory };
        return services.AddSimpleBM25DbDbAsMemoryDb(config);
    }
}
