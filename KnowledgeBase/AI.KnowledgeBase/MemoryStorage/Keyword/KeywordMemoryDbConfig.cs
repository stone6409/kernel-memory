// Copyright (c) Microsoft. All rights reserved.

using Microsoft.KernelMemory.FileSystem.DevTools;

namespace AI.KnowledgeBase.MemoryStorage.Keyword;

/// <summary>
/// Configuration for SimpleBM25Db
/// </summary>
public class KeywordMemoryDbConfig : MemoryDbConfig
{
    /// <summary>
    /// Gets a volatile storage configuration for SimpleBM25Db
    /// </summary>
    public new static KeywordMemoryDbConfig Volatile { get => new() { StorageType = FileSystemTypes.Volatile, Directory = "tmp-memory-text" }; }

    /// <summary>
    /// Gets a persistent storage configuration for SimpleBM25Db
    /// </summary>
    public new static KeywordMemoryDbConfig Persistent { get => new() { StorageType = FileSystemTypes.Disk, Directory = "tmp-memory-text" }; }

    /// <summary>
    /// Default constructor
    /// </summary>
    public KeywordMemoryDbConfig()
    {
        Directory = "tmp-memory-text";
    }
}
