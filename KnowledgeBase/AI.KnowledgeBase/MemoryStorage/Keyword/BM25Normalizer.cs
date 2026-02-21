namespace AI.KnowledgeBase.MemoryStorage.Keyword
{
    public static class BM25Normalizer
    {
        /// <summary>
        /// 将 BM25 分数归一化到 [0, 1] 范围
        /// </summary>
        public static Dictionary<string, double> NormalizeScores(Dictionary<string, double> scores)
        {
            if (scores.Count == 0)
                return new Dictionary<string, double>();

            var maxScore = scores.Values.Max();
            if (maxScore <= 0)
                return scores.ToDictionary(kvp => kvp.Key, kvp => 0.0);

            return scores.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value / maxScore
            );
        }

        /// <summary>
        /// 使用 softmax 归一化（概率分布）
        /// </summary>
        public static Dictionary<string, double> SoftmaxNormalize(Dictionary<string, double> scores)
        {
            if (scores.Count == 0)
                return new Dictionary<string, double>();

            // 防止数值溢出
            var maxScore = scores.Values.Max();
            var expScores = scores.ToDictionary(
                kvp => kvp.Key,
                kvp => Math.Exp(kvp.Value - maxScore)
            );

            var sumExp = expScores.Values.Sum();

            return expScores.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value / sumExp
            );
        }

        /// <summary>
        /// 使用 min-max 归一化到指定范围
        /// </summary>
        public static Dictionary<string, double> MinMaxNormalize(
            Dictionary<string, double> scores,
            double minRange = 0,
            double maxRange = 1)
        {
            if (scores.Count == 0)
                return new Dictionary<string, double>();

            var minScore = scores.Values.Min();
            var maxScore = scores.Values.Max();

            if (Math.Abs(maxScore - minScore) < double.Epsilon)
                return scores.ToDictionary(kvp => kvp.Key, kvp => minRange);

            return scores.ToDictionary(
                kvp => kvp.Key,
                kvp => minRange + (kvp.Value - minScore) * (maxRange - minRange) / (maxScore - minScore)
            );
        }
    }
}
