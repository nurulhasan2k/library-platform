namespace Library.Core.Services;

/// <summary>Thrown when a request breaks a library rule. The message is written to be shown to the person using the system.</summary>
public sealed class LibraryException : Exception
{
    public LibraryException(string message) : base(message) { }
}
