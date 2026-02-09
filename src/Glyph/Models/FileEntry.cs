namespace Glyph.Models;

public enum FileChangeKind
{
    Modified,
    Added,
    Deleted,
    Renamed,
    TypeChanged
}

public record FileEntry(string FilePath, FileChangeKind ChangeKind);
