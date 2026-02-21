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
    public static IKernelMemoryBuilder WithSimpleBM25DbDb(this IKernelMemoryBuilder builder, SimpleBM25DbConfig? config = null)
    {
        builder.Services.AddSimpleBM25DbDbAsMemoryDb(config ?? new SimpleBM25DbConfig());
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
    public static IServiceCollection AddSimpleBM25DbDbAsMemoryDb(this IServiceCollection services, SimpleBM25DbConfig? config = null)
    {
        return services
            .AddSingleton<SimpleBM25DbConfig>(config ?? new SimpleBM25DbConfig())
            .AddSingleton<IMemoryDb, SimpleBM25Db>();
    }

    public static IServiceCollection AddSimpleBM25DbDbAsMemoryDb(this IServiceCollection services, string directory)
    {
        var config = new SimpleBM25DbConfig { StorageType = FileSystemTypes.Disk, Directory = directory };
        return services.AddSimpleBM25DbDbAsMemoryDb(config);
    }
}
