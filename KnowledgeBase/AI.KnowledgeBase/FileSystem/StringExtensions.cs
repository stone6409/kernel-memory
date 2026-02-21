// Copyright (c) Microsoft. All rights reserved.

namespace AI.KnowledgeBase.FileSystem;

public static class StringExtensions
{
    public static string RemoveBOM(this string x)
    {
        return x.TrimStart('\uFEFF', '\u200B');
    }
}
