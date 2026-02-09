namespace Glyph.Models;

public record BranchInfo(
    string Name,
    string? ParentBranch,
    bool IsCurrent,
    bool IsRemote,
    int AheadOfParent,
    int BehindParent,
    string? LastCommitMessage,
    DateTimeOffset? LastCommitDate);
