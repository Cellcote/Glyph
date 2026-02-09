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

    public static bool IsGitRepository(string? path = null)
    {
        return Repository.Discover(path ?? Directory.GetCurrentDirectory()) != null;
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

    public IReadOnlyList<FileEntry> GetUnstagedFiles()
    {
        var status = _repo.RetrieveStatus(new StatusOptions { IncludeUntracked = true });
        var files = new List<FileEntry>();

        foreach (var entry in status)
        {
            var changeKind = entry.State switch
            {
                FileStatus.ModifiedInWorkdir => FileChangeKind.Modified,
                FileStatus.NewInWorkdir => FileChangeKind.Added,
                FileStatus.DeletedFromWorkdir => FileChangeKind.Deleted,
                FileStatus.RenamedInWorkdir => FileChangeKind.Renamed,
                FileStatus.TypeChangeInWorkdir => FileChangeKind.TypeChanged,
                // Include files that are both staged and have workdir changes
                var s when s.HasFlag(FileStatus.ModifiedInWorkdir) => FileChangeKind.Modified,
                var s when s.HasFlag(FileStatus.NewInWorkdir) => FileChangeKind.Added,
                var s when s.HasFlag(FileStatus.DeletedFromWorkdir) => FileChangeKind.Deleted,
                var s when s.HasFlag(FileStatus.RenamedInWorkdir) => FileChangeKind.Renamed,
                var s when s.HasFlag(FileStatus.TypeChangeInWorkdir) => FileChangeKind.TypeChanged,
                _ => (FileChangeKind?)null
            };

            if (changeKind.HasValue)
                files.Add(new FileEntry(entry.FilePath, changeKind.Value));
        }

        return files.OrderBy(f => f.FilePath).ToList();
    }

    public string? GetMergeBase(string branchName, string parentName)
    {
        var branch = _repo.Branches[branchName];
        var parent = _repo.Branches[parentName];

        if (branch?.Tip == null || parent?.Tip == null)
            return null;

        var mergeBase = _repo.ObjectDatabase.FindMergeBase(branch.Tip, parent.Tip);
        return mergeBase?.Sha;
    }

    public IReadOnlyList<CommitEntry> GetBranchCommits(string branchName, string parentName)
    {
        var mergeBase = GetMergeBase(branchName, parentName);
        if (mergeBase == null)
            return [];

        var branch = _repo.Branches[branchName];
        var mergeBaseCommit = _repo.Lookup<Commit>(mergeBase);

        var filter = new CommitFilter
        {
            IncludeReachableFrom = branch.Tip,
            ExcludeReachableFrom = mergeBaseCommit,
            SortBy = CommitSortStrategies.Topological | CommitSortStrategies.Reverse
        };

        return _repo.Commits.QueryBy(filter)
            .Select(c => new CommitEntry(c.Sha[..7], c.Sha, c.MessageShort))
            .ToList();
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
