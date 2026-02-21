// Copyright (c) Microsoft. All rights reserved.

using Microsoft.KernelMemory.FileSystem.DevTools;

namespace AI.KnowledgeBase.MemoryStorage.Vector;

/// <summary>
/// Configuration for SimpleVectorDb
/// </summary>
public class SimpleVectorDbConfig : SimpleMemoryDbConfig
{
    /// <summary>
    /// Gets a volatile storage configuration for SimpleVectorDb
    /// </summary>
    public new static SimpleVectorDbConfig Volatile { get => new() { StorageType = FileSystemTypes.Volatile, Directory = "tmp-memory-vectors" }; }

    /// <summary>
    /// Gets a persistent storage configuration for SimpleVectorDb
    /// </summary>
    public new static SimpleVectorDbConfig Persistent { get => new() { StorageType = FileSystemTypes.Disk, Directory = "tmp-memory-vectors" }; }

    /// <summary>
    /// Default constructor
    /// </summary>
    public SimpleVectorDbConfig()
    {
        Directory = "tmp-memory-vectors";
    }
}
