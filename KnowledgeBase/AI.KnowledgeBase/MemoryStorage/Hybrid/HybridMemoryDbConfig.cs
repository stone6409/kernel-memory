// Copyright (c) Microsoft. All rights reserved.

using Microsoft.KernelMemory.FileSystem.DevTools;

namespace AI.KnowledgeBase.MemoryStorage.Hybrid;

/// <summary>
/// Configuration for HybridMemoryDb
/// </summary>
public class HybridMemoryDbConfig : MemoryDbConfig
{
    /// <summary>
    /// Gets a volatile storage configuration for HybridMemoryDb
    /// </summary>
    public new static HybridMemoryDbConfig Volatile { get => new() { StorageType = FileSystemTypes.Volatile, Directory = "tmp-memory-hybrid" }; }

    /// <summary>
    /// Gets a persistent storage configuration for HybridMemoryDb
    /// </summary>
    public new static HybridMemoryDbConfig Persistent { get => new() { StorageType = FileSystemTypes.Disk, Directory = "tmp-memory-hybrid" }; }

    /// <summary>
    /// Default constructor
    /// </summary>
    public HybridMemoryDbConfig()
    {
        Directory = "tmp-memory-hybrid";
    }
}