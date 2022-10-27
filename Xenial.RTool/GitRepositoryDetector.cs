using System.IO.Abstractions;

namespace Xenial.RTool;

public sealed record GitRepositoryDetector(IFileSystem FileSystem)
{
    public bool DetectGitRepository(string? cd = null)
    {
        cd = cd ?? Environment.CurrentDirectory;
        var directoryInfo = FileSystem.DirectoryInfo.FromDirectoryName(cd);

        static bool Locate(IDirectoryInfo? directoryInfo)
        {
            if (directoryInfo is null)
            {
                return false;
            }

            foreach (var file in directoryInfo.EnumerateDirectories("*.*", SearchOption.TopDirectoryOnly))
            {
                if (file.Name.Equals(".git", StringComparison.InvariantCultureIgnoreCase))
                {
                    return true;
                }
            }

            return Locate(directoryInfo.Parent);
        }

        return Locate(directoryInfo);
    }
}
