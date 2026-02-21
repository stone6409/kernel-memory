// Copyright (c) Microsoft. All rights reserved.

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
    public FileSystemTypes StorageType { get; set; } = FileSystemTypes.Volatile;

    /// <summary>
    /// Directory of the storage.
    /// </summary>
    public string Directory { get; set; } = "tmp-memory-storage";

    /// <summary>
    /// Gets a volatile storage configuration
    /// </summary>
    public static T Volatile<T>() where T : MemoryDbConfig, new()
    {
        return new T { StorageType = FileSystemTypes.Volatile };
    }

    /// <summary>
    /// Gets a persistent storage configuration
    /// </summary>
    public static T Persistent<T>() where T : MemoryDbConfig, new()
    {
        return new T { StorageType = FileSystemTypes.Disk };
    }
}
