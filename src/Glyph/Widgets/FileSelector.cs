using Glyph.Models;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace Glyph.Widgets;

public class FileSelector
{
    private readonly IReadOnlyList<FileEntry> _files;
    private readonly HashSet<int> _selected = new();
    private int _cursor;
    private int _scrollOffset;
    private bool _treeView;

    public FileSelector(IReadOnlyList<FileEntry> files)
    {
        _files = files;
    }

    public FileSelectorResult Run()
    {
        if (Console.IsInputRedirected)
        {
            AnsiConsole.MarkupLine("[red]Error:[/] Interactive mode requires a terminal.");
            return new FileSelectorResult([], false);
        }

        var cursorWasVisible = true;
        try
        {
            try { Console.CursorVisible = false; }
            catch { cursorWasVisible = false; }

            FileSelectorResult? result = null;

            AnsiConsole.Live(new Markup(""))
                .AutoClear(true)
                .Start(ctx =>
                {
                    while (true)
                    {
                        ctx.UpdateTarget(BuildView());
                        ctx.Refresh();

                        var key = Console.ReadKey(true);
                        switch (key.Key)
                        {
                            case ConsoleKey.UpArrow:
                                if (_cursor > 0) _cursor--;
                                EnsureCursorVisible();
                                break;

                            case ConsoleKey.DownArrow:
                                if (_cursor < _files.Count - 1) _cursor++;
                                EnsureCursorVisible();
                                break;

                            case ConsoleKey.Spacebar:
                                if (!_selected.Remove(_cursor))
                                    _selected.Add(_cursor);
                                break;

                            case ConsoleKey.Tab:
                                _treeView = !_treeView;
                                break;

                            case ConsoleKey.A when key.Modifiers == 0:
                            case ConsoleKey.A when key.Modifiers == ConsoleModifiers.Shift:
                                if (_selected.Count == _files.Count)
                                    _selected.Clear();
                                else
                                    for (int i = 0; i < _files.Count; i++)
                                        _selected.Add(i);
                                break;

                            case ConsoleKey.D when key.Modifiers == 0:
                            case ConsoleKey.D when key.Modifiers == ConsoleModifiers.Shift:
                                result = new FileSelectorResult(
                                    [_files[_cursor].FilePath],
                                    false,
                                    Discard: true,
                                    DiscardChangeKind: _files[_cursor].ChangeKind);
                                return;

                            case ConsoleKey.Enter:
                                var paths = _selected
                                    .OrderBy(i => i)
                                    .Select(i => _files[i].FilePath)
                                    .ToList();
                                result = new FileSelectorResult(paths, false);
                                return;

                            case ConsoleKey.Escape:
                                result = new FileSelectorResult([], true);
                                return;
                        }
                    }
                });

            return result ?? new FileSelectorResult([], true);
        }
        finally
        {
            try { Console.CursorVisible = cursorWasVisible; }
            catch { /* terminal may not support it */ }
        }
    }

    private int ViewportHeight => Math.Max(Console.WindowHeight - 6, 5);

    private void EnsureCursorVisible()
    {
        if (_cursor < _scrollOffset)
            _scrollOffset = _cursor;
        else if (_cursor >= _scrollOffset + ViewportHeight)
            _scrollOffset = _cursor - ViewportHeight + 1;
    }

    private IRenderable BuildView()
    {
        var lines = new List<IRenderable>();

        lines.Add(new Markup(
            "[dim]↑↓[/] navigate  [dim]Space[/] select  [dim]A[/] all  " +
            "[dim]D[/] discard  [dim]Tab[/] view  [dim]Enter[/] confirm  [dim]Esc[/] cancel"));
        lines.Add(new Markup(""));

        if (_treeView)
            BuildTreeLines(lines);
        else
            BuildFlatLines(lines);

        var total = _files.Count;
        var selectedCount = _selected.Count;
        lines.Add(new Markup(""));
        lines.Add(new Markup($"[dim]{selectedCount}/{total} selected[/]"));

        return new Rows(lines);
    }

    private void BuildFlatLines(List<IRenderable> lines)
    {
        var viewport = ViewportHeight;
        var end = Math.Min(_scrollOffset + viewport, _files.Count);

        if (_scrollOffset > 0)
            lines.Add(new Markup($"[dim]  ↑ {_scrollOffset} more above[/]"));

        for (int i = _scrollOffset; i < end; i++)
        {
            var file = _files[i];
            var isCursor = i == _cursor;
            var isSelected = _selected.Contains(i);

            var cursor = isCursor ? ">" : " ";
            var check = isSelected ? "[green]x[/]" : " ";
            var tag = FormatTag(file.ChangeKind);
            var path = Markup.Escape(file.FilePath);

            var style = isCursor ? "bold" : "default";
            lines.Add(new Markup($"[{style}]{cursor} [{(isSelected ? "green" : "default")}]\\[[{check}]][/] {tag} {path}[/]"));
        }

        var remaining = _files.Count - end;
        if (remaining > 0)
            lines.Add(new Markup($"[dim]  ↓ {remaining} more below[/]"));
    }

    private void BuildTreeLines(List<IRenderable> lines)
    {
        // Group files by directory
        var groups = _files
            .Select((f, i) => (File: f, Index: i))
            .GroupBy(x => GetDirectory(x.File.FilePath))
            .OrderBy(g => g.Key);

        // Build a flat list of visible items: directories + files
        var treeItems = new List<(int FileIndex, string Display, int Indent)>();
        foreach (var group in groups)
        {
            var dirParts = group.Key == "."
                ? Array.Empty<string>()
                : group.Key.Split('/');

            if (dirParts.Length > 0)
                treeItems.Add((-1, string.Join("/", dirParts) + "/", 0));

            foreach (var item in group.OrderBy(x => x.File.FilePath))
            {
                var fileName = Path.GetFileName(item.File.FilePath);
                treeItems.Add((item.Index, fileName, dirParts.Length > 0 ? 1 : 0));
            }
        }

        // Map cursor (file index) to tree position
        var viewport = ViewportHeight;
        var cursorTreePos = treeItems.FindIndex(t => t.FileIndex == _cursor);
        var treeOffset = 0;
        if (cursorTreePos >= 0)
        {
            if (cursorTreePos < treeOffset)
                treeOffset = cursorTreePos;
            else if (cursorTreePos >= treeOffset + viewport)
                treeOffset = cursorTreePos - viewport + 1;
        }

        var end = Math.Min(treeOffset + viewport, treeItems.Count);

        if (treeOffset > 0)
            lines.Add(new Markup($"[dim]  ↑ more above[/]"));

        for (int i = treeOffset; i < end; i++)
        {
            var (fileIndex, display, indent) = treeItems[i];
            var indentStr = new string(' ', indent * 2);

            if (fileIndex < 0)
            {
                // Directory header
                lines.Add(new Markup($"[blue]{indentStr}{Markup.Escape(display)}[/]"));
            }
            else
            {
                var file = _files[fileIndex];
                var isCursor = fileIndex == _cursor;
                var isSelected = _selected.Contains(fileIndex);

                var cursor = isCursor ? ">" : " ";
                var check = isSelected ? "[green]x[/]" : " ";
                var tag = FormatTag(file.ChangeKind);

                var style = isCursor ? "bold" : "default";
                lines.Add(new Markup(
                    $"[{style}]{cursor} [{(isSelected ? "green" : "default")}]\\[[{check}]][/] {tag} {indentStr}{Markup.Escape(display)}[/]"));
            }
        }

        var remaining = treeItems.Count - end;
        if (remaining > 0)
            lines.Add(new Markup($"[dim]  ↓ more below[/]"));
    }

    private static string GetDirectory(string filePath)
    {
        var dir = Path.GetDirectoryName(filePath)?.Replace('\\', '/');
        return string.IsNullOrEmpty(dir) ? "." : dir;
    }

    private static string FormatTag(FileChangeKind kind) => kind switch
    {
        FileChangeKind.Modified => "[yellow]M[/]",
        FileChangeKind.Added => "[green]A[/]",
        FileChangeKind.Deleted => "[red]D[/]",
        FileChangeKind.Renamed => "[blue]R[/]",
        FileChangeKind.TypeChanged => "[cyan]T[/]",
        _ => "[dim]?[/]"
    };
}

public record FileSelectorResult(
    IReadOnlyList<string> SelectedPaths,
    bool Cancelled,
    bool Discard = false,
    FileChangeKind? DiscardChangeKind = null);
