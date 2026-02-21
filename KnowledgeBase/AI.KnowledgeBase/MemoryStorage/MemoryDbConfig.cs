// Copyright (c) Microsoft. All rights reserved.

using AI.KnowledgeBase.FileSystem;
using Microsoft.KernelMemory.FileSystem.DevTools;

namespace AI.KnowledgeBase.MemoryStorage;

/// <summary>
/// Base configuration for simple memory databases
/// </summary>
public abstract class MemoryDbConfig
{
    /// <summary>
    /// The type of storage to use. Defaults to volatile (in RAM).
    /// </summary>
    public EnhancedFileSystemTypes StorageType { get; set; } = EnhancedFileSystemTypes.Volatile;

    /// <summary>
    /// Directory of the storage.
    /// </summary>
    public string Directory { get; set; } = "tmp-memory-storage";

    /// <summary>
    /// Gets a volatile storage configuration
    /// </summary>
    public static T Volatile<T>() where T : MemoryDbConfig, new()
    {
        return new T { StorageType = EnhancedFileSystemTypes.Volatile };
    }

    /// <summary>
    /// Gets a persistent storage configuration
    /// </summary>
    public static T Persistent<T>() where T : MemoryDbConfig, new()
    {
        return new T { StorageType = EnhancedFileSystemTypes.Disk };
    }
}
