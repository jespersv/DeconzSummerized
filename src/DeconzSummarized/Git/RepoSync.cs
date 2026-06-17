using LibGit2Sharp;
using LibGit2Sharp.Handlers;

namespace DeconzSummarized.Git;

/// <summary>Thin wrapper over LibGit2Sharp for clone/pull and commit/push with PAT auth.</summary>
public sealed class RepoSync
{
    private readonly string _user;
    private readonly string _pat;

    public RepoSync(string user, string pat)
    {
        _user = user;
        _pat = pat;
    }

    private CredentialsHandler Credentials =>
        (_url, _usernameFromUrl, _types) =>
            new UsernamePasswordCredentials { Username = _user, Password = _pat };

    /// <summary>Clones the repo into <paramref name="dir"/>, or pulls if it already exists there.</summary>
    public void CloneOrPull(string url, string dir, Signature signature)
    {
        if (Repository.IsValid(dir))
        {
            using var repo = new Repository(dir);
            var pullOptions = new PullOptions
            {
                FetchOptions = new FetchOptions { CredentialsProvider = Credentials },
            };
            Commands.Pull(repo, signature, pullOptions);
            return;
        }

        Directory.CreateDirectory(dir);
        var cloneOptions = new CloneOptions();
        cloneOptions.FetchOptions.CredentialsProvider = Credentials;
        Repository.Clone(url, dir, cloneOptions);
    }

    /// <summary>
    /// Stages everything in <paramref name="dir"/> and commits + pushes to <paramref name="branch"/>.
    /// Returns false (and does nothing) when there is nothing to commit.
    /// </summary>
    public bool CommitAndPush(string dir, string message, string branch, Signature author)
    {
        using var repo = new Repository(dir);

        Commands.Stage(repo, "*");

        if (!repo.RetrieveStatus().IsDirty)
            return false;

        repo.Commit(message, author, author);

        var remote = repo.Network.Remotes["origin"];
        var pushOptions = new PushOptions { CredentialsProvider = Credentials };
        // HEAD:refs/heads/<branch> works whether or not the local branch tracks a remote.
        repo.Network.Push(remote, $"HEAD:refs/heads/{branch}", pushOptions);
        return true;
    }
}
