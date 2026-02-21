/// <summary>
/// BM25 algorithm parameters
/// </summary>
namespace AI.KnowledgeBase.MemoryStorage.BM25
{
    public class BM25Parameters
    {
        /// <summary>
        /// BM25 k1 parameter (controls term frequency saturation)
        /// </summary>
        public double K1 { get; set; } = 1.2;

        /// <summary>
        /// BM25 b parameter (controls document length normalization)
        /// </summary>
        public double B { get; set; } = 0.75;

        /// <summary>
        /// BM25 delta parameter (for BM25+ variant)
        /// </summary>
        public double Delta { get; set; } = 1.0;

        /// <summary>
        /// Minimum term frequency for scoring
        /// </summary>
        public int MinTermFrequency { get; set; } = 1;

        /// <summary>
        /// Whether to use BM25+ variant (adds delta to IDF)
        /// </summary>
        public bool UseBM25Plus { get; set; } = true;

        /// <summary>
        /// Whether to apply length normalization
        /// </summary>
        public bool UseLengthNormalization { get; set; } = true;
    }
}
