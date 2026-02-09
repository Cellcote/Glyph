using Glyph.Services;

namespace Glyph.Tests;

public class GitServiceTests
{
    [Fact]
    public void IsGitRepository_ReturnsFalse_ForNonGitDirectory()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            Assert.False(GitService.IsGitRepository(tempDir));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void IsGitRepository_ReturnsTrue_ForGitDirectory()
    {
        // The Glyph repo itself is a git repository
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        Assert.True(GitService.IsGitRepository(repoRoot));
    }

    [Fact]
    public void Constructor_ThrowsInvalidOperationException_ForNonGitDirectory()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            Assert.Throws<InvalidOperationException>(() => new GitService(tempDir));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }
}
