# Glyph

A Git TUI for trunk-based development workflows. Glyph gives you a branch-centric view of your repository, with shortcuts for the operations you do most: rebase on parent, create a PR, sync with upstream.

## What makes Glyph different

Most Git TUIs are focused on repo state — staging files, viewing diffs, browsing logs. Glyph is **workflow-focused**: every branch knows its parent, and every command operates relative to that parent. This makes trunk-based development and stacked branches feel natural.

## Install

Requires [.NET 10](https://dotnet.microsoft.com/download) SDK.

```bash
git clone git@github.com:Cellcote/Glyph.git
cd Glyph
dotnet build
```

Run from the build output:

```bash
dotnet run --project src/Glyph -- --help
```

## Commands

| Command | Description |
|---------|-------------|
| `glyph` | Show the branch tree (default) |
| `glyph tree` | Show branch tree with parent relationships and ahead/behind counts |
| `glyph parent [branch]` | Get or set the parent branch for the current branch |
| `glyph rebase` | Fetch and rebase current branch onto its parent |
| `glyph pr` | Push and create a pull request into the parent branch |
| `glyph sync` | Fetch all remotes and rebase onto parent |
| `glyph stack` | Show the full branch stack from current branch to trunk |

### Parent branch tracking

Glyph stores parent relationships in git config:

```bash
# Set the parent of your current branch
glyph parent main

# Check the current parent
glyph parent
# → Parent of feature-x: main
```

The default parent is `main`. When you create stacked branches, set the parent to keep the chain intact:

```
main → feature-auth → feature-auth-ui → feature-auth-tests
```

### Creating PRs

Glyph uses the [GitHub CLI](https://cli.github.com/) (`gh`) under the hood. Make sure it's installed and authenticated:

```bash
gh auth login
glyph pr                        # PR into parent branch
glyph pr --title "Add auth"     # PR with custom title
```

## Tech stack

- [System.CommandLine](https://learn.microsoft.com/en-us/dotnet/standard/commandline/) — CLI framework
- [Spectre.Console](https://spectreconsole.net/) — Terminal rendering (trees, tables, spinners)
- [LibGit2Sharp](https://github.com/libgit2/libgit2sharp) — Git operations without shelling out

## License

MIT
