using Glyph.Models;
using Spectre.Console;

namespace Glyph.Services;

public static class TreeRenderer
{
    public static void Render(IReadOnlyList<BranchInfo> branches, string defaultParent)
    {
        var tree = new Tree($"[bold yellow]{defaultParent}[/]");

        // Group branches by parent
        var byParent = branches
            .Where(b => b.Name != defaultParent)
            .GroupBy(b => b.ParentBranch ?? defaultParent)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Find the trunk branch info
        var trunk = branches.FirstOrDefault(b => b.Name == defaultParent);
        if (trunk != null)
        {
            tree.Style = Style.Parse("yellow");
        }

        AddChildren(tree, defaultParent, byParent, branches);

        AnsiConsole.Write(tree);
    }

    private static void AddChildren(
        IHasTreeNodes parent,
        string parentName,
        Dictionary<string, List<BranchInfo>> byParent,
        IReadOnlyList<BranchInfo> allBranches)
    {
        if (!byParent.TryGetValue(parentName, out var children))
            return;

        foreach (var branch in children.OrderBy(b => b.Name))
        {
            var label = FormatBranchLabel(branch);
            var node = parent.AddNode(label);
            AddChildren(node, branch.Name, byParent, allBranches);
        }
    }

    private static string FormatBranchLabel(BranchInfo branch)
    {
        var marker = branch.IsCurrent ? "[bold green]* [/]" : "  ";
        var name = branch.IsCurrent
            ? $"[bold green]{branch.Name}[/]"
            : $"[blue]{branch.Name}[/]";

        var stats = new List<string>();
        if (branch.AheadOfParent > 0)
            stats.Add($"[green]+{branch.AheadOfParent}[/]");
        if (branch.BehindParent > 0)
            stats.Add($"[red]-{branch.BehindParent}[/]");

        var statsStr = stats.Count > 0 ? $" ({string.Join(" ", stats)})" : "";

        var commit = branch.LastCommitMessage != null
            ? $" [dim]{Markup.Escape(Truncate(branch.LastCommitMessage, 50))}[/]"
            : "";

        return $"{marker}{name}{statsStr}{commit}";
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..(maxLength - 1)] + "\u2026";
}
