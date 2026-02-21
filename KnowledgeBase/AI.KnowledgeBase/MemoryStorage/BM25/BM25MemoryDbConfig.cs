// Copyright (c) Microsoft. All rights reserved.

using Microsoft.KernelMemory.FileSystem.DevTools;

namespace AI.KnowledgeBase.MemoryStorage.BM25;

/// <summary>
/// Configuration for SimpleBM25Db
/// </summary>
public class BM25MemoryDbConfig : MemoryDbConfig
{
    /// <summary>
    /// Gets a volatile storage configuration for SimpleBM25Db
    /// </summary>
    public new static BM25MemoryDbConfig Volatile { get => new() { StorageType = FileSystemTypes.Volatile, Directory = "tmp-memory-text" }; }

    /// <summary>
    /// Gets a persistent storage configuration for SimpleBM25Db
    /// </summary>
    public new static BM25MemoryDbConfig Persistent { get => new() { StorageType = FileSystemTypes.Disk, Directory = "tmp-memory-text" }; }

    /// <summary>
    /// Default constructor
    /// </summary>
    public BM25MemoryDbConfig()
    {
        Directory = "tmp-memory-text";
    }
}
