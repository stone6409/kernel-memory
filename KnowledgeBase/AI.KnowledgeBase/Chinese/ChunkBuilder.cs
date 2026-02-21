
using System.Text;

namespace AI.KnowledgeBase.Chinese
{
    internal class ChunkBuilder
    {
        public readonly StringBuilder FullContent = new();
        public readonly StringBuilder NextSentence = new();
    }
}
