namespace Xenial.RTool;

public interface IGitRepositoryDetector
{
    bool DetectGitRepository(string? cd = null);
}
public interface IGitRepositoryRootDetector
{
    string? DetectGitRootDirectory(string? cd = null);
}