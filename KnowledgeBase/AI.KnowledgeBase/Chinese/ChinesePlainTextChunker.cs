
using System.Text;
using Microsoft.KernelMemory.AI;
using Microsoft.KernelMemory.Chunkers;
using Microsoft.KernelMemory.DataFormats;
using Microsoft.KernelMemory.Text;

namespace AI.KnowledgeBase.Chinese
{
    public class ChinesePlainTextChunker
    {
        internal enum SeparatorTypes
        {
            ExplicitSeparator,
            PotentialSeparator,
            WeakSeparator1,
            WeakSeparator2,
            WeakSeparator3,
            NotASeparator,
        }

        // Do not allow chunks smaller than this size, to avoid unnecessary computation.
        // Realistically, a chunk should be at least 1000 tokens long.
        private const int MinChunkSize = 5;

        private readonly ITextTokenizer _tokenizer;

        //// Prioritized list of characters to split sentence from sentence.
        //private static readonly SeparatorTrie s_explicitSeparators = new([
        //    // Symbol + space
        //    ". ", ".\t", ".\n", "\n\n", // note: covers also the case of multiple '.' like "....\n"
        //    "? ", "?\t", "?\n", // note: covers also the case of multiple '?' and '!?' like "?????\n" and "?!?\n"
        //    "! ", "!\t", "!\n", // note: covers also the case of multiple '!' and '?!' like "!!!\n" and "!?!\n"
        //    "⁉ ", "⁉\t", "⁉\n",
        //    "⁈ ", "⁈\t", "⁈\n",
        //    "⁇ ", "⁇\t", "⁇\n",
        //    "… ", "…\t", "…\n",
        //    // Multi-char separators without space, ordered by length
        //    "!!!!", "????", "!!!", "???", "?!?", "!?!", "!?", "?!", "!!", "??", "....", "...", "..",
        //    // 1 char separators without space
        //    ".", "?", "!", "⁉", "⁈", "⁇", "…",
        //]);

        private static readonly SeparatorTrie s_explicitSeparators = new([
            // 英文标点 + 空格/制表符/换行
            ". ", ".\t", ".\n", "\n\n",
            "? ", "?\t", "?\n",
            "! ", "!\t", "!\n",

            // 中文标点 + 空格/制表符/换行（新增）
            "。 ", "。\t", "。\n",
            "？ ", "？\t", "？\n",
            "！ ", "！\t", "！\n",
            "； ", "；\t", "；\n",
            "： ", "：\t", "：\n",
            "， ", "，\t", "，\n",

            // Unicode标点符号 + 空格/制表符/换行（保留）
            "⁉ ", "⁉\t", "⁉\n",
            "⁈ ", "⁈\t", "⁈\n",
            "⁇ ", "⁇\t", "⁇\n",
            "… ", "…\t", "…\n",

            // 多字符分隔符（英文）
            "!!!!", "????", "!!!", "???", "?!?", "!?!", "!?", "?!", "!!", "??", "....", "...", "..",

            // 单字符分隔符（英文 + Unicode + 中文）
            ".", "?", "!", "⁉", "⁈", "⁇", "…",  // 英文和Unicode
            "。", "？", "！", "；", "：", "，",  // 中文（新增）
        ]);

        //// Prioritized list of characters to split inside a sentence.
        //private static readonly SeparatorTrie s_potentialSeparators = new([
        //    "; ", ";\t", ";\n", ";",
        //    "} ", "}\t", "}\n", "}", // note: curly brace without spaces is up here because it's a common code ending char, more important than ')' or ']'
        //    ") ", ")\t", ")\n",
        //    "] ", "]\t", "]\n",
        //    ")", "]",
        //]);

        private static readonly SeparatorTrie s_potentialSeparators = new([
            // 英文
            "; ", ";\t", ";\n", ";",
            "} ", "}\t", "}\n", "}",
            ") ", ")\t", ")\n",
            "] ", "]\t", "]\n",
            ")", "]",

            // 中文（新增）
            "； ", "；\t", "；\n", "；",
            "） ", "）\t", "）\n", "）",
            "】 ", "】\t", "】\n", "】",
        ]);

        //// Prioritized list of characters to split inside a sentence when other splits are not found.
        //private static readonly SeparatorTrie s_weakSeparators1 = new([
        //    ": ", ":", // note: \n \t make no difference with this char
        //    ", ", ",", // note: \n \t make no difference with this char
        //]);

        private static readonly SeparatorTrie s_weakSeparators1 = new([
            // 英文
            ": ", ":",
            ", ", ",",

            // 中文（新增）
            "： ", "：",
            "， ", "，",
        ]);

        // Prioritized list of characters to split inside a sentence when other splits are not found.
        private static readonly SeparatorTrie s_weakSeparators2 = new([
            "\n", // note: \n \t make no difference with this char
            "\t", // note: \n \t make no difference with this char
            "' ", "'", // note: \n \t make no difference with this char
            "\" ", "\"", // note: \n \t make no difference with this char
            " ", // note: \n \t make no difference with this char
        ]);

        // Prioritized list of characters to split inside a sentence when other splits are not found.
        private static readonly SeparatorTrie s_weakSeparators3 = new([
            "_", // note: \n \t make no difference with this char
            "-", // note: \n \t make no difference with this char
            "|", // note: \n \t make no difference with this char
            "@", // note: \n \t make no difference with this char
            "=", // note: \n \t make no difference with this char
        ]);

        public ChinesePlainTextChunker(ITextTokenizer? tokenizer = null)
        {
            this._tokenizer = tokenizer ?? new ChineseTokenizer(); // 使用中文分词
        }

        /// <summary>
        /// Split plain text into chunks of text.
        /// </summary>
        /// <param name="text">Text to split</param>
        /// <param name="maxTokensPerChunk">Maximum number of tokens per chunk (must be > 0)</param>
        /// <returns>List of chunks.</returns>
        public List<string> Split(string text, int maxTokensPerChunk)
        {
            return this.Split(text, new PlainTextChunkerOptions { MaxTokensPerChunk = maxTokensPerChunk });
        }

        /// <summary>
        /// Split plain text into blocks.
        /// Note:
        /// - \r\n characters are replaced with \n
        /// - \r characters are replaced with \n
        /// - \t character is not replaced
        /// - Chunks cannot be smaller than [MinChunkSize] tokens (header excluded)
        /// </summary>
        /// <param name="text">Text to split</param>
        /// <param name="options">How to handle input and how to generate chunks</param>
        /// <returns>List of chunks.</returns>
        public List<string> Split(string text, PlainTextChunkerOptions options)
        {
            ArgumentNullException.ThrowIfNull(text);
            ArgumentNullException.ThrowIfNull(options);

            // Clean up text. Note: LLMs don't use \r char
            text = text.NormalizeNewlines(true);

            // Calculate chunk size leaving room for the optional chunk header
            int maxChunk1Size = options.MaxTokensPerChunk - this.TokenCount(options.ChunkHeader);
            int maxChunkNSize = options.MaxTokensPerChunk - this.TokenCount(options.ChunkHeader) - options.Overlap;
            maxChunk1Size = Math.Max(MinChunkSize, maxChunk1Size);
            maxChunkNSize = Math.Max(MinChunkSize, maxChunkNSize);

            // Chunk using recursive logic, starting with explicit separators and moving to weaker ones if needed
            bool firstChunkDone = false;
            var chunks = this.RecursiveSplit(text, maxChunk1Size, maxChunkNSize, SeparatorTypes.ExplicitSeparator, ref firstChunkDone);

            // Add overlapping tokens. Note: won't copy more than maxChunkSize (no exceptions thrown)
            if (options.Overlap > 0 && chunks.Count > 1)
            {
                var newChunks = new List<string> { chunks[0] };

                for (int index = 1; index < chunks.Count; index++)
                {
                    // Tokenize the previous chunk, and copy last N tokens into the next chunk
                    IReadOnlyList<string> previousChunkTokens = this._tokenizer.GetTokens(chunks[index - 1]);
                    IEnumerable<string> overlapTokens = previousChunkTokens.Skip(previousChunkTokens.Count - options.Overlap);
                    newChunks.Add($"{string.Join("", overlapTokens)}{chunks[index]}");
                }

                chunks = newChunks;
            }

            // Add header to each chunk
            if (!string.IsNullOrEmpty(options.ChunkHeader))
            {
                chunks = chunks.Select(x => $"{options.ChunkHeader}{x}").ToList();
            }

            return chunks;
        }

        internal static SeparatorTypes NextSeparatorType(SeparatorTypes separatorType)
        {
            switch (separatorType)
            {
                case SeparatorTypes.ExplicitSeparator: return SeparatorTypes.PotentialSeparator;
                case SeparatorTypes.PotentialSeparator: return SeparatorTypes.WeakSeparator1;
                case SeparatorTypes.WeakSeparator1: return SeparatorTypes.WeakSeparator2;
                case SeparatorTypes.WeakSeparator2: return SeparatorTypes.WeakSeparator3;
                case SeparatorTypes.WeakSeparator3: return SeparatorTypes.NotASeparator;
                default: throw new ArgumentOutOfRangeException(nameof(SeparatorTypes.NotASeparator) + " doesn't have a next separator type.");
            }
        }

        /// <summary>
        /// Greedy algorithm aggregating fragments into chunks separated by a specific separator type.
        /// If any of the generated chunks is too long, those are split recursively using weaker separators.
        /// </summary>
        /// <param name="text">Text to split</param>
        /// <param name="maxChunk1Size">Max size of first chunk</param>
        /// <param name="maxChunkNSize">Max size of each chunk</param>
        /// <param name="separatorType">Type of separator to detect</param>
        /// <param name="firstChunkDone">Used to know if we're processing the first chunk, e.g. for overlapping</param>
        /// <returns>List of strings</returns>
        internal List<string> RecursiveSplit(
            string text,
            int maxChunk1Size,
            int maxChunkNSize,
            SeparatorTypes separatorType,
            ref bool firstChunkDone)
        {
            // Edge case: empty text
            if (string.IsNullOrEmpty(text)) { return []; }

            // Edge case: text is already short enough
            var maxChunkSize = firstChunkDone ? maxChunkNSize : maxChunk1Size;
            if (this.TokenCount(text) <= maxChunkSize) { return [text]; }

            // Important: 'SplitToFragments' splits content in words and delimiters, using logic specific to plain text.
            //            These are different from LLM tokens, which are based on the tokenizer used to train the model.
            // Recursive logic exit clause: when separator type is NotASeparator, count each char as a fragment
            List<Chunk> fragments = separatorType switch
            {
                SeparatorTypes.ExplicitSeparator => this.SplitToFragments(text, s_explicitSeparators),
                SeparatorTypes.PotentialSeparator => this.SplitToFragments(text, s_potentialSeparators),
                SeparatorTypes.WeakSeparator1 => this.SplitToFragments(text, s_weakSeparators1),
                SeparatorTypes.WeakSeparator2 => this.SplitToFragments(text, s_weakSeparators2),
                SeparatorTypes.WeakSeparator3 => this.SplitToFragments(text, s_weakSeparators3),
                SeparatorTypes.NotASeparator => this.SplitToFragments(text, null),
                _ => throw new ArgumentOutOfRangeException(nameof(separatorType), separatorType, null)
            };

            return this.GenerateChunks(fragments, maxChunk1Size, maxChunkNSize, separatorType, ref firstChunkDone);
        }

        internal List<string> GenerateChunks(
            List<Chunk> fragments,
            int maxChunk1Size,
            int maxChunkNSize,
            SeparatorTypes separatorType,
            ref bool firstChunkDone)
        {
            if (fragments.Count == 0) { return []; }

            var chunks = new List<string>();
            var chunk = new ChunkBuilder();
            int maxChunkSize;

            foreach (var fragment in fragments)
            {
                // Note: fragments != LLM tokens. One fragment can contain multiple tokens.
                chunk.NextSentence.Append(fragment.Content);

                // Keep adding until a separator is found
                if (!fragment.IsSeparator) { continue; }

                string nextSentence = chunk.NextSentence.ToString();
                int nextSentenceSize = this.TokenCount(nextSentence);
                maxChunkSize = firstChunkDone ? maxChunkNSize : maxChunk1Size;

                // Detect current state
                // 1:
                // - the current chunk is still empty
                // - the next sentence is complete and is NOT too long
                // 2:
                // - the current chunk is still empty
                // - the next sentence is complete and is TOO LONG
                // 3:
                // - the current chunk is NOT empty
                // - the next sentence is complete and is NOT too long
                // 4:
                // - the current chunk is NOT empty
                // - the next sentence is complete and is TOO LONG
                int state;
                if (chunk.FullContent.Length == 0)
                {
                    state = (nextSentenceSize <= maxChunkSize) ? 1 : 2;
                }
                else
                {
                    state = (nextSentenceSize <= maxChunkSize) ? 3 : 4;
                }

                switch (state)
                {
                    default:
                        throw new ArgumentOutOfRangeException(nameof(state));

                    // - the current chunk is still empty
                    // - the next sentence is complete and is NOT too long
                    case 1:
                        chunk.FullContent.Append(nextSentence);
                        chunk.NextSentence.Clear();
                        continue;

                    // - the current chunk is still empty
                    // - the next sentence is complete and is TOO LONG
                    case 2:
                    {
                        var moreChunks = this.RecursiveSplit(nextSentence, maxChunk1Size, maxChunkNSize, NextSeparatorType(separatorType), ref firstChunkDone);
                        chunks.AddRange(moreChunks.Take(moreChunks.Count - 1));
                        chunk.NextSentence.Clear().Append(moreChunks.Last());
                        continue;
                    }

                    // - the current chunk is NOT empty
                    // - the next sentence is complete and is NOT too long
                    case 3:
                    {
                        var chunkPlusSentence = $"{chunk.FullContent}{chunk.NextSentence}";
                        if (this.TokenCount(chunkPlusSentence) <= maxChunkSize)
                        {
                            // Move next sentence to current chunk
                            chunk.FullContent.Append(chunk.NextSentence);
                        }
                        else
                        {
                            // Complete the current chunk and start a new one
                            AddChunk(chunks, chunk.FullContent.ToString(), ref firstChunkDone);
                            chunk.FullContent.Clear().Append(chunk.NextSentence);
                        }

                        chunk.NextSentence.Clear();
                        continue;
                    }

                    // - the current chunk is NOT empty
                    // - the next sentence is complete and is TOO LONG
                    case 4:
                    {
                        AddChunk(chunks, chunk.FullContent, ref firstChunkDone);

                        var moreChunks = this.RecursiveSplit(nextSentence, maxChunk1Size, maxChunkNSize, NextSeparatorType(separatorType), ref firstChunkDone);
                        chunks.AddRange(moreChunks.Take(moreChunks.Count - 1));
                        chunk.NextSentence.Clear().Append(moreChunks.Last());
                        continue;
                    }
                }
            }

            // If there's something left in the buffers
            string fullSentenceLeft = chunk.FullContent.ToString();
            string nextSentenceLeft = chunk.NextSentence.ToString();
            maxChunkSize = firstChunkDone ? maxChunkNSize : maxChunk1Size;

            if (fullSentenceLeft.Length > 0 || nextSentenceLeft.Length > 0)
            {
                if (this.TokenCount($"{fullSentenceLeft}{nextSentenceLeft}") <= maxChunkSize)
                {
                    AddChunk(chunks, $"{fullSentenceLeft}{nextSentenceLeft}", ref firstChunkDone);
                }
                else
                {
                    if (fullSentenceLeft.Length > 0)
                    {
                        AddChunk(chunks, fullSentenceLeft, ref firstChunkDone);
                    }

                    if (nextSentenceLeft.Length > 0)
                    {
                        if (this.TokenCount(nextSentenceLeft) < maxChunkSize)
                        {
                            AddChunk(chunks, nextSentenceLeft, ref firstChunkDone);
                        }
                        else
                        {
                            var moreChunks = this.RecursiveSplit(nextSentenceLeft, maxChunk1Size, maxChunkNSize, NextSeparatorType(separatorType), ref firstChunkDone);
                            chunks.AddRange(moreChunks);
                        }
                    }
                }
            }

            return chunks;
        }

        /// <summary>
        /// Split text into fragments using a list of separators.
        /// </summary>
        internal List<Chunk> SplitToFragments(string text, SeparatorTrie? separators)
        {
            // Split all chars
            if (separators == null)
            {
                return text.Select(x => new Chunk(x, -1) { IsSeparator = true }).ToList();
            }

            // If the text is empty or there are no separators
            if (string.IsNullOrEmpty(text) || separators.Length == 0) { return []; }

            var fragments = new List<Chunk>();
            var fragmentBuilder = new StringBuilder();
            int index = 0;
            while (index < text.Length)
            {
                string? foundSeparator = separators.MatchLongest(text, index);

                if (foundSeparator != null)
                {
                    if (fragmentBuilder.Length > 0)
                    {
                        fragments.Add(new Chunk(fragmentBuilder, -1) { IsSeparator = false });
                        fragmentBuilder.Clear();
                    }

                    fragments.Add(new Chunk(foundSeparator, -1) { IsSeparator = true });
                    index += foundSeparator.Length;
                }
                else
                {
                    fragmentBuilder.Append(text[index]);
                    index++;
                }
            }

            if (fragmentBuilder.Length > 0)
            {
                fragments.Add(new Chunk(fragmentBuilder, -1) { IsSeparator = false });
            }

            return fragments;
        }

        private int TokenCount(string? input)
        {
            if (input == null) { return 0; }

            return this._tokenizer.CountTokens(input);
        }

        private static void AddChunk(List<string> chunks, StringBuilder chunk, ref bool firstChunkDone)
        {
            chunks.Add(chunk.ToString());
            chunk.Clear();
            firstChunkDone = true;
        }

        private static void AddChunk(List<string> chunks, string chunk, ref bool firstChunkDone)
        {
            chunks.Add(chunk);
            firstChunkDone = true;
        }
    }
}
