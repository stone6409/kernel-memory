/// <summary>
/// BM25Document representation for BM25 calculation
/// </summary>

namespace AI.KnowledgeBase.MemoryStorage
{
    public class BM25Document
    {
        /// <summary>
        /// BM25Document identifier
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Original bM25Document text
        /// </summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>
        /// Tokenized terms from the bM25Document
        /// </summary>
        public List<string> Tokens { get; set; } = new List<string>();

        /// <summary>
        /// Additional metadata associated with the bM25Document
        /// </summary>
        public object? Metadata { get; set; }
    }
}
