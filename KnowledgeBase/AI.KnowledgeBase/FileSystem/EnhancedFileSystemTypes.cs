namespace AI.KnowledgeBase.FileSystem
{
    /// <summary>
    /// The type of storage to use.
    /// </summary>
    public enum EnhancedFileSystemTypes
    {
        /// <summary>
        /// Save data to disk.
        /// </summary>
        Disk,

        /// <summary>
        /// Save data to memory.
        /// </summary>
        Volatile,

        /// <summary>
        /// Hybrid file system combining disk persistence with memory caching.
        /// Provides both persistence and high-speed access for frequently used data.
        /// </summary>
        Hybrid
    }
}
