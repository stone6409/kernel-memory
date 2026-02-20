
using System.Text;
using Microsoft.KernelMemory.AI;

/// <summary>
/// 中文专用Tokenizer，按字符分词
/// </summary>
namespace AI.KnowledgeBase.Chinese
{
    public class ChineseTokenizer : ITextTokenizer
    {
        public int CountTokens(string text)
        {
            if (string.IsNullOrEmpty(text))
                return 0;

            // 中文按字符计数，英文按单词计数
            int tokenCount = 0;
            bool inEnglishWord = false;

            foreach (char c in text)
            {
                if (IsChineseCharacter(c))
                {
                    // 每个中文字符算一个token
                    tokenCount++;
                    inEnglishWord = false;
                }
                else if (char.IsLetterOrDigit(c))
                {
                    // 英文单词开始或继续
                    if (!inEnglishWord)
                    {
                        tokenCount++;
                        inEnglishWord = true;
                    }
                }
                else
                {
                    // 标点符号、空格等
                    tokenCount++;
                    inEnglishWord = false;
                }
            }

            return tokenCount;
        }

        public IReadOnlyList<string> GetTokens(string text)
        {
            if (string.IsNullOrEmpty(text))
                return Array.Empty<string>();

            var tokens = new List<string>();
            var currentToken = new StringBuilder();

            foreach (char c in text)
            {
                if (IsChineseCharacter(c))
                {
                    // 中文字符单独作为一个token
                    if (currentToken.Length > 0)
                    {
                        tokens.Add(currentToken.ToString());
                        currentToken.Clear();
                    }
                    tokens.Add(c.ToString());
                }
                else if (char.IsLetterOrDigit(c))
                {
                    // 英文单词累积
                    currentToken.Append(c);
                }
                else
                {
                    // 标点符号等
                    if (currentToken.Length > 0)
                    {
                        tokens.Add(currentToken.ToString());
                        currentToken.Clear();
                    }
                    tokens.Add(c.ToString());
                }
            }

            // 处理最后一个token
            if (currentToken.Length > 0)
            {
                tokens.Add(currentToken.ToString());
            }

            return tokens;
        }

        private bool IsChineseCharacter(char c)
        {
            // Unicode范围：基本汉字、扩展A区、扩展B区等
            return (c >= 0x4E00 && c <= 0x9FFF) || 
                   (c >= 0x3400 && c <= 0x4DBF) || 
                   (c >= 0x20000 && c <= 0x2A6DF) ||
                   (c >= 0x2A700 && c <= 0x2B73F) ||
                   (c >= 0x2B740 && c <= 0x2B81F) ||
                   (c >= 0x2B820 && c <= 0x2CEAF) ||
                   (c >= 0xF900 && c <= 0xFAFF) ||
                   (c >= 0x2F800 && c <= 0x2FA1F);
        }
    }
}
