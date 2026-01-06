namespace TTT.Exceptions;

public sealed class RailReferenceImportException : Exception
{
    public string CorpusPath { get; }
    public string BplanPath { get; }

    public RailReferenceImportException(string message, string corpusPath, string bplanPath) : base(message)
    {
        CorpusPath = corpusPath;
        BplanPath = bplanPath;
    }
}
