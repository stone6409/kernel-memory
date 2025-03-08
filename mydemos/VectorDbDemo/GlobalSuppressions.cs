// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("Performance", "CA1861:Avoid constant arrays as arguments", Justification = "<Pending>", Scope = "member", Target = "~M:VectorDbDemo.Program.Main(System.String[])~System.Threading.Tasks.Task")]
[assembly: SuppressMessage("Reliability", "CA2007:Consider calling ConfigureAwait on the awaited task", Justification = "<Pending>")]
[assembly: SuppressMessage("Globalization", "CA1303:Do not pass literals as localized parameters", Justification = "<Pending>")]
[assembly: SuppressMessage("Performance", "CA1861:Avoid constant arrays as arguments", Justification = "<Pending>", Scope = "member", Target = "~M:VectorDbDemo.Program.DefineAndImportIndexesAsync(VectorDbDemo.RAGService)~System.Threading.Tasks.Task")]
[assembly: SuppressMessage("Style", "IDE0009:Member access should be qualified.", Justification = "<Pending>", Scope = "member", Target = "~M:VectorDbDemo.RAGService.ImportDocumentsFromFolderAsync(System.String,System.Collections.Generic.IEnumerable{System.String},System.Collections.Generic.IEnumerable{System.String},System.String)~System.Threading.Tasks.Task{System.Int32}")]
[assembly: SuppressMessage("Globalization", "CA1307:Specify StringComparison for clarity", Justification = "<Pending>", Scope = "member", Target = "~M:VectorDbDemo.RAGService.ImportDocumentsFromFolderAsync(System.String,System.Collections.Generic.IEnumerable{System.String},System.Collections.Generic.IEnumerable{System.String},System.String)~System.Threading.Tasks.Task{System.Int32}")]
[assembly: SuppressMessage("Style", "IDE0009:Member access should be qualified.", Justification = "<Pending>", Scope = "member", Target = "~M:VectorDbDemo.RAGService.ImportDocumentAsync(System.String,System.String,System.String)~System.Threading.Tasks.Task{System.String}")]
