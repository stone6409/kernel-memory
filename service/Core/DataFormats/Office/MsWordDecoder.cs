﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.Extensions.Logging;
using Microsoft.KernelMemory.Diagnostics;
using Microsoft.KernelMemory.Pipeline;
using Microsoft.KernelMemory.Text;

namespace Microsoft.KernelMemory.DataFormats.Office;

[Experimental("KMEXP00")]
public sealed class MsWordDecoder : IContentDecoder
{
    private readonly ILogger<MsWordDecoder> _log;

    // 关键点：Word文档解码器
    public MsWordDecoder(ILoggerFactory? loggerFactory = null)
    {
        this._log = (loggerFactory ?? DefaultLogger.Factory).CreateLogger<MsWordDecoder>();
    }

    /// <inheritdoc />
    public bool SupportsMimeType(string mimeType)
    {
        return mimeType != null && mimeType.StartsWith(MimeTypes.MsWordX, StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public Task<FileContent> DecodeAsync(string filename, CancellationToken cancellationToken = default)
    {
        using var stream = File.OpenRead(filename);
        return this.DecodeAsync(stream, cancellationToken);
    }

    /// <inheritdoc />
    public Task<FileContent> DecodeAsync(BinaryData data, CancellationToken cancellationToken = default)
    {
        using var stream = data.ToStream();
        return this.DecodeAsync(stream, cancellationToken);
    }

    /// <inheritdoc />
    public Task<FileContent> DecodeAsync(Stream data, CancellationToken cancellationToken = default)
    {
        this._log.LogDebug("Extracting text from MS Word file");

        var result = new FileContent(MimeTypes.PlainText);
        var wordprocessingDocument = WordprocessingDocument.Open(data, false);
        try
        {
            StringBuilder sb = new();

            MainDocumentPart? mainPart = wordprocessingDocument.MainDocumentPart;
            if (mainPart is null)
            {
                throw new InvalidOperationException("The main document part is missing.");
            }

            Body? body = mainPart.Document.Body;
            if (body is null)
            {
                throw new InvalidOperationException("The document body is missing.");
            }

            int pageNumber = 1;
            IEnumerable<Paragraph>? paragraphs = body.Descendants<Paragraph>();
            if (paragraphs != null)
            {
                foreach (Paragraph p in paragraphs)
                {
                    // Note: this is just an attempt at counting pages, not 100% reliable
                    // see https://stackoverflow.com/questions/39992870/how-to-access-openxml-content-by-page-number
                    var lastRenderedPageBreak = p.GetFirstChild<Run>()?.GetFirstChild<LastRenderedPageBreak>();
                    if (lastRenderedPageBreak != null)
                    {
                        // Note: no trimming, use original spacing when working with pages
                        string pageContent = sb.ToString().NormalizeNewlines(false);
                        sb.Clear();
                        result.Sections.Add(new Chunk(pageContent, pageNumber, Chunk.Meta(sentencesAreComplete: true)));
                        pageNumber++;
                    }

                    sb.AppendLineNix(p.InnerText);
                }
            }

            // Note: no trimming, use original spacing when working with pages
            string lastPageContent = sb.ToString().NormalizeNewlines(false);
            result.Sections.Add(new Chunk(lastPageContent, pageNumber, Chunk.Meta(sentencesAreComplete: true)));

            return Task.FromResult(result);
        }
        finally
        {
            wordprocessingDocument.Dispose();
        }
    }
}
