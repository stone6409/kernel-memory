// Copyright (c) Microsoft. All rights reserved.

using Microsoft.KernelMemory.FileSystem.DevTools;

namespace AI.KnowledgeBase.MemoryStorage.BM25;

/// <summary>
/// Configuration for SimpleBM25Db
/// </summary>
public class SimpleBM25DbConfig : SimpleMemoryDbConfig
{
    /// <summary>
    /// Gets a volatile storage configuration for SimpleBM25Db
    /// </summary>
    public new static SimpleBM25DbConfig Volatile { get => new() { StorageType = FileSystemTypes.Volatile, Directory = "tmp-memory-text" }; }

    /// <summary>
    /// Gets a persistent storage configuration for SimpleBM25Db
    /// </summary>
    public new static SimpleBM25DbConfig Persistent { get => new() { StorageType = FileSystemTypes.Disk, Directory = "tmp-memory-text" }; }

    /// <summary>
    /// Default constructor
    /// </summary>
    public SimpleBM25DbConfig()
    {
        Directory = "tmp-memory-text";
    }
}