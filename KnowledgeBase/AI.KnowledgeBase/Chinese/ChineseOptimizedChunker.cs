// 创建中文优化的分块器

using System.Text;
using Microsoft.KernelMemory.AI;
using Microsoft.KernelMemory.Chunkers;

namespace AI.KnowledgeBase.Chinese
{
    public class ChineseOptimizedChunker : PlainTextChunker
    {
        private readonly ITextTokenizer _chineseTokenizer;

        public ChineseOptimizedChunker(ITextTokenizer? tokenizer = null) 
            : base(tokenizer ?? new ChineseTokenizer())
        {
            _chineseTokenizer = tokenizer ?? new ChineseTokenizer();
        }

        /// <summary>
        /// 重写分块方法，添加中文优化
        /// </summary>
        public List<string> Split(string text, PlainTextChunkerOptions options)
        {
            if (string.IsNullOrEmpty(text))
                return new List<string>();

            // 预处理中文文本
            text = PreprocessChineseText(text);

            // 使用基类分块
            var chunks = base.Split(text, options);

            // 后处理：修复重叠导致的字符重复
            chunks = FixChineseOverlapDuplicates(chunks);

            return chunks;
        }

        /// <summary>
        /// 预处理中文文本
        /// </summary>
        private string PreprocessChineseText(string text)
        {
            // 统一标点符号
            var result = new StringBuilder();
            foreach (char c in text)
            {
                char normalized = c switch
                {
                    ',' => '，',
                    '.' => '。',
                    '!' => '！',
                    '?' => '？',
                    ';' => '；',
                    ':' => '：',
                    _ => c
                };
                result.Append(normalized);
            }

            return result.ToString();
        }

        /// <summary>
        /// 修复中文重叠重复问题
        /// </summary>
        private List<string> FixChineseOverlapDuplicates(List<string> chunks)
        {
            if (chunks.Count <= 1) return chunks;

            var fixedChunks = new List<string> { chunks[0] };

            for (int i = 1; i < chunks.Count; i++)
            {
                string previous = chunks[i - 1];
                string current = chunks[i];

                // 找到重叠部分
                int overlapLength = FindOverlapLength(previous, current);

                if (overlapLength > 0)
                {
                    string overlap = current.Substring(0, overlapLength);
                    string fixedOverlap = FixCharacterDuplicates(overlap);

                    if (overlap != fixedOverlap)
                    {
                        current = fixedOverlap + current.Substring(overlapLength);
                    }
                }

                fixedChunks.Add(current);
            }

            return fixedChunks;
        }

        private int FindOverlapLength(string str1, string str2)
        {
            int maxOverlap = Math.Min(str1.Length, str2.Length);
            for (int i = maxOverlap; i > 0; i--)
            {
                if (str1.EndsWith(str2.Substring(0, i)))
                    return i;
            }
            return 0;
        }

        private string FixCharacterDuplicates(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            var result = new StringBuilder();
            char? lastChar = null;

            foreach (char c in text)
            {
                if (c != lastChar)
                {
                    result.Append(c);
                    lastChar = c;
                }
                else
                {
                    // 检查是否是合理的中文重复
                    if (IsValidChineseDuplicate(c))
                    {
                        result.Append(c);
                    }
                    // 否则跳过重复字符
                }
            }

            return result.ToString();
        }

        private bool IsValidChineseDuplicate(char c)
        {
            // 中文中常见的重复字符
            string validDuplicates = "慢慢常常渐渐纷纷匆匆默默悄悄明明恰恰刚刚好好";
            return validDuplicates.Contains(c);
        }
    }
}
