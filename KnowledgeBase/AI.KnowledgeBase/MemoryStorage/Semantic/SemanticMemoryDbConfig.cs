// Copyright (c) Microsoft. All rights reserved.

using AI.KnowledgeBase.FileSystem;
using Microsoft.KernelMemory.FileSystem.DevTools;

namespace AI.KnowledgeBase.MemoryStorage.Semantic;

/// <summary>
/// Configuration for SimpleVectorDb
/// </summary>
public class SemanticMemoryDbConfig : MemoryDbConfig
{
    /// <summary>
    /// Gets a volatile storage configuration for SimpleVectorDb
    /// </summary>
    public new static SemanticMemoryDbConfig Volatile { get => new() { StorageType = EnhancedFileSystemTypes.Volatile, Directory = "tmp-memory-vectors" }; }

    /// <summary>
    /// Gets a persistent storage configuration for SimpleVectorDb
    /// </summary>
    public new static SemanticMemoryDbConfig Persistent { get => new() { StorageType = EnhancedFileSystemTypes.Disk, Directory = "tmp-memory-vectors" }; }

    /// <summary>
    /// Default constructor
    /// </summary>
    public SemanticMemoryDbConfig()
    {
        Directory = "tmp-memory-vectors";
    }
}
