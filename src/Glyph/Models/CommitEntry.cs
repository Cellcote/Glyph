namespace Glyph.Models;

public enum RebaseAction
{
    Pick,
    Drop,
    Squash,
    Reword
}

public class CommitEntry
{
    public string ShortHash { get; }
    public string FullHash { get; }
    public string Message { get; }
    public RebaseAction Action { get; set; } = RebaseAction.Pick;
    public string EditedMessage { get; set; }

    public CommitEntry(string shortHash, string fullHash, string message)
    {
        ShortHash = shortHash;
        FullHash = fullHash;
        Message = message;
        EditedMessage = message;
    }
}
