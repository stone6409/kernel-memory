// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.Diagnostics;
using Microsoft.KernelMemory.Pipeline;

namespace AI.KnowledgeBase.FileSystem;

/// <summary>
/// Hybrid file system with lazy loading: read from cache, write to both.
/// </summary>
internal sealed class HybridFileSystem : IFileSystem
{
    private const string DefaultVolumeName = "__default__";
    private static readonly Regex s_invalidCharsRegex = new(@"[\s|\||\\|/|\0|'|\`|""|:|;|,|~|!|?|*|+|=|^|@|#|$|%|&]");

    private readonly ILogger _log;
    private readonly IMimeTypeDetection _mimeTypeDetection;
    private readonly DiskFileSystem _diskFileSystem;
    private readonly VolatileFileSystem _volatileFileSystem;
    private readonly ConcurrentDictionary<string, bool> _loadedFiles = new();

    /// <summary>
    /// Create a new hybrid file system
    /// </summary>
    public HybridFileSystem(
        string directory,
        IMimeTypeDetection? mimeTypeDetection = null,
        ILoggerFactory? loggerFactory = null)
    {
        this._mimeTypeDetection = mimeTypeDetection ?? new MimeTypesDetection();
        this._log = (loggerFactory ?? DefaultLogger.Factory).CreateLogger<HybridFileSystem>();
        
        // Initialize both file systems
        this._diskFileSystem = new DiskFileSystem(directory, mimeTypeDetection, loggerFactory);
        this._volatileFileSystem = VolatileFileSystem.GetInstance(directory, mimeTypeDetection, loggerFactory);
    }

    #region Volume API

    /// <inheritdoc />
    public async Task CreateVolumeAsync(string volume, CancellationToken cancellationToken = default)
    {
        volume = ValidateVolumeName(volume);
        
        // Create volume in both systems
        await this._diskFileSystem.CreateVolumeAsync(volume, cancellationToken).ConfigureAwait(false);
        await this._volatileFileSystem.CreateVolumeAsync(volume, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> VolumeExistsAsync(string volume, CancellationToken cancellationToken = default)
    {
        volume = ValidateVolumeName(volume);
        
        // Check disk (source of truth)
        return await this._diskFileSystem.VolumeExistsAsync(volume, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task DeleteVolumeAsync(string volume, CancellationToken cancellationToken = default)
    {
        volume = ValidateVolumeName(volume);
        
        // Delete from both systems
        await this._diskFileSystem.DeleteVolumeAsync(volume, cancellationToken).ConfigureAwait(false);
        await this._volatileFileSystem.DeleteVolumeAsync(volume, cancellationToken).ConfigureAwait(false);
        
        // Clear loaded files for this volume
        ClearLoadedFilesForVolume(volume);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<string>> ListVolumesAsync(CancellationToken cancellationToken = default)
    {
        // Get volumes from disk (source of truth)
        return await this._diskFileSystem.ListVolumesAsync(cancellationToken).ConfigureAwait(false);
    }

    #endregion

    #region Directory API

    /// <inheritdoc />
    public async Task CreateDirectoryAsync(string volume, string relPath, CancellationToken cancellationToken = default)
    {
        volume = ValidateVolumeName(volume);
        relPath = ValidatePath(relPath);
        
        // Create directory in both systems
        await this._diskFileSystem.CreateDirectoryAsync(volume, relPath, cancellationToken).ConfigureAwait(false);
        await this._volatileFileSystem.CreateDirectoryAsync(volume, relPath, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task DeleteDirectoryAsync(string volume, string relPath, CancellationToken cancellationToken = default)
    {
        volume = ValidateVolumeName(volume);
        relPath = ValidatePath(relPath);
        
        // Get all files in directory
        var fileNames = await this._diskFileSystem.GetAllFileNamesAsync(volume, relPath, cancellationToken).ConfigureAwait(false);
        
        // Delete from both systems
        await this._diskFileSystem.DeleteDirectoryAsync(volume, relPath, cancellationToken).ConfigureAwait(false);
        await this._volatileFileSystem.DeleteDirectoryAsync(volume, relPath, cancellationToken).ConfigureAwait(false);
        
        // Clear loaded files for files in this directory
        foreach (var fileName in fileNames)
        {
            var cacheKey = GetCacheKey(volume, relPath, fileName);
            _loadedFiles.TryRemove(cacheKey, out _);
        }
    }

    #endregion

    #region File API

    /// <inheritdoc />
    public async Task WriteFileAsync(string volume, string relPath, string fileName, Stream streamContent, CancellationToken cancellationToken = default)
    {
        volume = ValidateVolumeName(volume);
        relPath = ValidatePath(relPath);
        fileName = ValidateFileName(fileName);
        
        // Read stream content
        byte[] content;
        await using (streamContent.ConfigureAwait(false))
        {
            content = streamContent.ReadAllBytes();
        }
        
        // Write to disk (primary storage)
        await using (var memoryStream = new MemoryStream(content))
        {
            await this._diskFileSystem.WriteFileAsync(volume, relPath, fileName, memoryStream, cancellationToken).ConfigureAwait(false);
        }
        
        // Write to cache
        await using (var memoryStream = new MemoryStream(content))
        {
            await this._volatileFileSystem.WriteFileAsync(volume, relPath, fileName, memoryStream, cancellationToken).ConfigureAwait(false);
        }
        
        // Mark as loaded
        var cacheKey = GetCacheKey(volume, relPath, fileName);
        _loadedFiles[cacheKey] = true;
    }

    /// <inheritdoc />
    public async Task WriteFileAsync(string volume, string relPath, string fileName, string data, CancellationToken cancellationToken = default)
    {
        volume = ValidateVolumeName(volume);
        relPath = ValidatePath(relPath);
        fileName = ValidateFileName(fileName);
        
        // Write to disk (primary storage)
        await this._diskFileSystem.WriteFileAsync(volume, relPath, fileName, data, cancellationToken).ConfigureAwait(false);
        
        // Write to cache
        await this._volatileFileSystem.WriteFileAsync(volume, relPath, fileName, data, cancellationToken).ConfigureAwait(false);
        
        // Mark as loaded
        var cacheKey = GetCacheKey(volume, relPath, fileName);
        _loadedFiles[cacheKey] = true;
    }

    /// <inheritdoc />
    public async Task<BinaryData> ReadFileAsBinaryAsync(string volume, string relPath, string fileName, CancellationToken cancellationToken = default)
    {
        volume = ValidateVolumeName(volume);
        relPath = ValidatePath(relPath);
        fileName = ValidateFileName(fileName);
        
        var cacheKey = GetCacheKey(volume, relPath, fileName);
        
        // Check if file is already loaded in cache
        if (_loadedFiles.ContainsKey(cacheKey))
        {
            // Read from cache
            try
            {
                return await this._volatileFileSystem.ReadFileAsBinaryAsync(volume, relPath, fileName, cancellationToken).ConfigureAwait(false);
            }
            catch (FileNotFoundException)
            {
                // Cache miss, continue to disk
            }
        }
        
        // Read from disk
        var diskData = await this._diskFileSystem.ReadFileAsBinaryAsync(volume, relPath, fileName, cancellationToken).ConfigureAwait(false);
        
        // Load into cache for future reads
        if (diskData != null)
        {
            await this._volatileFileSystem.WriteFileAsync(volume, relPath, fileName, diskData.ToStream(), cancellationToken).ConfigureAwait(false);
            _loadedFiles[cacheKey] = true;
        }
        
        return diskData;
    }

    /// <inheritdoc />
    public async Task<string> ReadFileAsTextAsync(string volume, string relPath, string fileName, CancellationToken cancellationToken = default)
    {
        var binaryData = await ReadFileAsBinaryAsync(volume, relPath, fileName, cancellationToken).ConfigureAwait(false);
        return binaryData.ToString();
    }

    /// <inheritdoc />
    public async Task<StreamableFileContent> ReadFileInfoAsync(string volume, string relPath, string fileName, CancellationToken cancellationToken = default)
    {
        volume = ValidateVolumeName(volume);
        relPath = ValidatePath(relPath);
        fileName = ValidateFileName(fileName);
        
        var cacheKey = GetCacheKey(volume, relPath, fileName);
        
        // Check if file is already loaded in cache
        if (_loadedFiles.ContainsKey(cacheKey))
        {
            // Try to get info from cache
            try
            {
                return await this._volatileFileSystem.ReadFileInfoAsync(volume, relPath, fileName, cancellationToken).ConfigureAwait(false);
            }
            catch (FileNotFoundException)
            {
                // Cache miss, continue to disk
            }
        }
        
        // Get info from disk
        var diskInfo = await this._diskFileSystem.ReadFileInfoAsync(volume, relPath, fileName, cancellationToken).ConfigureAwait(false);
        
        return diskInfo;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<string>> GetAllFileNamesAsync(string volume, string relPath, CancellationToken cancellationToken = default)
    {
        volume = ValidateVolumeName(volume);
        relPath = ValidatePath(relPath);
        
        // Always get from disk (source of truth for directory structure)
        return await this._diskFileSystem.GetAllFileNamesAsync(volume, relPath, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> FileExistsAsync(string volume, string relPath, string fileName, CancellationToken cancellationToken = default)
    {
        volume = ValidateVolumeName(volume);
        relPath = ValidatePath(relPath);
        fileName = ValidateFileName(fileName);
        
        // Check disk (source of truth)
        return await this._diskFileSystem.FileExistsAsync(volume, relPath, fileName, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task DeleteFileAsync(string volume, string relPath, string fileName, CancellationToken cancellationToken = default)
    {
        volume = ValidateVolumeName(volume);
        relPath = ValidatePath(relPath);
        fileName = ValidateFileName(fileName);
        
        // Delete from both systems
        await this._diskFileSystem.DeleteFileAsync(volume, relPath, fileName, cancellationToken).ConfigureAwait(false);
        await this._volatileFileSystem.DeleteFileAsync(volume, relPath, fileName, cancellationToken).ConfigureAwait(false);
        
        // Clear loaded flag
        var cacheKey = GetCacheKey(volume, relPath, fileName);
        _loadedFiles.TryRemove(cacheKey, out _);
    }

    /// <inheritdoc />
    public async Task<IDictionary<string, string>> ReadAllFilesAsTextAsync(string volume, string relPath, CancellationToken cancellationToken = default)
    {
        volume = ValidateVolumeName(volume);
        relPath = ValidatePath(relPath);
        
        // Get all file names from disk
        var fileNames = await this._diskFileSystem.GetAllFileNamesAsync(volume, relPath, cancellationToken).ConfigureAwait(false);
        
        var result = new Dictionary<string, string>();
        
        // Read each file (will load into cache if not already loaded)
        foreach (var fileName in fileNames)
        {
            try
            {
                var content = await ReadFileAsTextAsync(volume, relPath, fileName, cancellationToken).ConfigureAwait(false);
                result[fileName] = content;
            }
            catch (Exception ex)
            {
                this._log.LogError(ex, "Error reading file '{FileName}'", fileName);
            }
        }
        
        return result;
    }

    #endregion

    #region Cache Management

    /// <summary>
    /// Clear the entire cache
    /// </summary>
    public async Task ClearCacheAsync(CancellationToken cancellationToken = default)
    {
        // Get all volumes from cache
        var volumes = await this._volatileFileSystem.ListVolumesAsync(cancellationToken).ConfigureAwait(false);
        
        foreach (var volume in volumes)
        {
            await this._volatileFileSystem.DeleteVolumeAsync(volume, cancellationToken).ConfigureAwait(false);
            await this._volatileFileSystem.CreateVolumeAsync(volume, cancellationToken).ConfigureAwait(false);
        }
        
        _loadedFiles.Clear();
    }

    /// <summary>
    /// Preload specific file into cache
    /// </summary>
    public async Task PreloadFileAsync(string volume, string relPath, string fileName, CancellationToken cancellationToken = default)
    {
        volume = ValidateVolumeName(volume);
        relPath = ValidatePath(relPath);
        fileName = ValidateFileName(fileName);
        
        var cacheKey = GetCacheKey(volume, relPath, fileName);
        
        // If not already loaded, load it
        if (!_loadedFiles.ContainsKey(cacheKey))
        {
            try
            {
                var data = await this._diskFileSystem.ReadFileAsBinaryAsync(volume, relPath, fileName, cancellationToken).ConfigureAwait(false);
                await this._volatileFileSystem.WriteFileAsync(volume, relPath, fileName, data.ToStream(), cancellationToken).ConfigureAwait(false);
                _loadedFiles[cacheKey] = true;
            }
            catch (FileNotFoundException)
            {
                // File doesn't exist, do nothing
            }
        }
    }

    /// <summary>
    /// Unload file from cache (keep on disk)
    /// </summary>
    public async Task UnloadFileAsync(string volume, string relPath, string fileName, CancellationToken cancellationToken = default)
    {
        volume = ValidateVolumeName(volume);
        relPath = ValidatePath(relPath);
        fileName = ValidateFileName(fileName);
        
        var cacheKey = GetCacheKey(volume, relPath, fileName);
        
        // Remove from cache
        await this._volatileFileSystem.DeleteFileAsync(volume, relPath, fileName, cancellationToken).ConfigureAwait(false);
        _loadedFiles.TryRemove(cacheKey, out _);
    }

    #endregion

    #region Private Methods

    private void ClearLoadedFilesForVolume(string volume)
    {
        var keysToRemove = _loadedFiles.Keys
            .Where(key => key.StartsWith($"{volume}|", StringComparison.OrdinalIgnoreCase))
            .ToList();
        
        foreach (var key in keysToRemove)
        {
            _loadedFiles.TryRemove(key, out _);
        }
    }

    private static string GetCacheKey(string volume, string relPath, string fileName)
    {
        return $"{volume}|{relPath}|{fileName}";
    }

    private static string ValidateVolumeName(string volume)
    {
        if (string.IsNullOrEmpty(volume))
        {
            return DefaultVolumeName;
        }

        if (s_invalidCharsRegex.Match(volume).Success)
        {
            throw new ArgumentException($"The volume name '{volume}' contains some invalid chars or empty spaces");
        }

        return volume;
    }

    private static string ValidatePath(string path)
    {
        if (path.Contains('\\', StringComparison.Ordinal))
        {
            throw new ArgumentException("The path contains some invalid chars: backslash '\\' chars are not allowed");
        }

        if (path.Contains(':', StringComparison.Ordinal))
        {
            throw new ArgumentException("The path contains some invalid chars: colon ':' chars are not allowed");
        }

        return path;
    }

    private static string ValidateFileName(string fileName)
    {
        if (fileName.Contains('/', StringComparison.Ordinal))
        {
            throw new ArgumentException($"The file name {fileName} contains some invalid chars: slash '/' chars are not allowed");
        }

        if (fileName.Contains('\\', StringComparison.Ordinal))
        {
            throw new ArgumentException($"The file name {fileName} contains some invalid chars: backslash '\\' chars are not allowed");
        }

        if (fileName.Contains(':', StringComparison.Ordinal))
        {
            throw new ArgumentException($"The file name {fileName} contains some invalid chars: colon ':' chars are not allowed");
        }

        return fileName;
    }

    #endregion
}