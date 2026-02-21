// Copyright (c) Microsoft. All rights reserved.

using Microsoft.KernelMemory.FileSystem.DevTools;

namespace AI.KnowledgeBase.MemoryStorage;

public class SimpleBM25DbConfig
{
    public static SimpleBM25DbConfig Volatile { get => new() { StorageType = FileSystemTypes.Volatile }; }

    public static SimpleBM25DbConfig Persistent { get => new() { StorageType = FileSystemTypes.Disk }; }

    /// <summary>
    /// The type of storage to use. Defaults to volatile (in RAM).
    /// </summary>
    public FileSystemTypes StorageType { get; set; } = FileSystemTypes.Volatile;

    /// <summary>
    /// Directory of the text file storage.
    /// </summary>
    public string Directory { get; set; } = "tmp-memory-text";
}
