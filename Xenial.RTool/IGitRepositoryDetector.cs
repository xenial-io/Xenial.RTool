namespace Xenial.RTool;

public interface IGitRepositoryDetector
{
    bool DetectGitRepository(string? cd = null);
}