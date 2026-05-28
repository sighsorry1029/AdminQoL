namespace AdminQoL;

internal readonly struct KnowledgeCommandResult
{
    internal bool Success { get; }
    internal string Message { get; }

    private KnowledgeCommandResult(bool success, string message)
    {
        Success = success;
        Message = message;
    }

    internal static KnowledgeCommandResult Ok(string message)
    {
        return new KnowledgeCommandResult(true, message);
    }

    internal static KnowledgeCommandResult Error(string message)
    {
        return new KnowledgeCommandResult(false, message);
    }
}
