namespace VectorDbDemo
{
    /// <summary>
    /// 文件夹配置类
    /// </summary>
    public class FolderConfig
    {
        public string FolderPath { get; set; } // 文件夹路径
        public string[] IncludePatterns { get; set; } // 包含的文件模式
        public string[] ExcludePaths { get; set; } // 排除的文件路径
    }
}
