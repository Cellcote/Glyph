using LibGit2Sharp;
using Glyph.Models;

namespace Glyph.Services;

public class GitService : IDisposable
{
    private readonly Repository _repo;
    private readonly GlyphConfig _config;

    public GitService(string? path = null, GlyphConfig? config = null)
    {
        var repoPath = Repository.Discover(path ?? Directory.GetCurrentDirectory())
            ?? throw new InvalidOperationException("Not a git repository (or any parent up to mount point).");
        _repo = new Repository(repoPath);
        _config = config ?? new GlyphConfig();
    }

    public string CurrentBranchName => _repo.Head.FriendlyName;

    public string GetParentBranch(string branchName)
    {
        var configKey = $"glyph.branch.{branchName}.parent";
        var entry = _repo.Config.Get<string>(configKey);
        return entry?.Value ?? _config.DefaultParent;
    }

    public void SetParentBranch(string branchName, string parentBranch)
    {
        var configKey = $"glyph.branch.{branchName}.parent";
        _repo.Config.Set(configKey, parentBranch);
    }

    public IReadOnlyList<BranchInfo> GetBranches()
    {
        var branches = new List<BranchInfo>();

        foreach (var branch in _repo.Branches.Where(b => !b.IsRemote))
        {
            var parentName = GetParentBranch(branch.FriendlyName);
            var parentBranch = _repo.Branches[parentName];

            int ahead = 0, behind = 0;
            if (parentBranch != null && branch.Tip != null && parentBranch.Tip != null)
            {
                var divergence = _repo.ObjectDatabase.CalculateHistoryDivergence(branch.Tip, parentBranch.Tip);
                ahead = divergence.AheadBy ?? 0;
                behind = divergence.BehindBy ?? 0;
            }

            var tip = branch.Tip;
            branches.Add(new BranchInfo(
                Name: branch.FriendlyName,
                ParentBranch: parentName,
                IsCurrent: branch.IsCurrentRepositoryHead,
                IsRemote: false,
                AheadOfParent: ahead,
                BehindParent: behind,
                LastCommitMessage: tip?.MessageShort,
                LastCommitDate: tip?.Author.When));
        }

        return branches;
    }

    public (int Ahead, int Behind) GetDivergence(string branchName, string parentName)
    {
        var branch = _repo.Branches[branchName];
        var parent = _repo.Branches[parentName];

        if (branch?.Tip == null || parent?.Tip == null)
            return (0, 0);

        var divergence = _repo.ObjectDatabase.CalculateHistoryDivergence(branch.Tip, parent.Tip);
        return (divergence.AheadBy ?? 0, divergence.BehindBy ?? 0);
    }

    public void Dispose()
    {
        _repo.Dispose();
        GC.SuppressFinalize(this);
    }
}
