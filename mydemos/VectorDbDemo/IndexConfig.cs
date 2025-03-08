namespace VectorDbDemo
{
    /// <summary>
    /// 索引配置类
    /// </summary>
    public class IndexConfig
    {
        public string IndexName { get; set; } // 索引名称
        public List<SingleFileConfig> SingleFiles { get; set; } // 单个文件配置
        public List<FolderConfig> FolderConfigs { get; set; } // 多个文件夹配置
    }
}
