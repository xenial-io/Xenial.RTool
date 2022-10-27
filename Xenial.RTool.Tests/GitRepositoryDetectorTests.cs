using System.IO.Abstractions.TestingHelpers;

using Shouldly;

using Xenial.RTool;

namespace Xenial.Tests.RTool;

public sealed class GitRepositoryDetectorTests
{
    [Fact]
    public void DetectGitRepository()
    {
        var root = OperatingSystem.IsWindows()
           ? @"c:\demo\"
           : "/var/demo/";

        var s = Path.DirectorySeparatorChar;

        var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
        {
            { @$"{root}myfile.txt", new MockFileData("Testing is meh.") },
            { @$"{root}.git{s}index.js", new MockFileData("some js") },
            { @$"{root}sub{s}image.gif", new MockFileData(new byte[] { 0x12, 0x34, 0x56, 0xd2 }) },
            { @$"{root}sub{s}sub{s}image.gif", new MockFileData(new byte[] { 0x12, 0x34, 0x56, 0xd2 }) },
            { @$"{root}sub{s}sub{s}sub{s}image.gif", new MockFileData(new byte[] { 0x12, 0x34, 0x56, 0xd2 }) },
        });

        var detector = new GitRepositoryDetector(fileSystem);
        var cd = @$"{root}sub{s}sub{s}sub{s}";
        var isRepo = detector.DetectGitRepository(cd);

        isRepo.ShouldBeTrue("Should be a git repository");
    }

    [Fact]
    public void DetectNotAGitRepository()
    {
        var root = OperatingSystem.IsWindows()
           ? @"c:\demo\"
           : "/var/demo/";

        var s = Path.DirectorySeparatorChar;

        var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
        {
            { @$"{root}myfile.txt", new MockFileData("Testing is meh.") },
            { @$"{root}sub{s}image.gif", new MockFileData(new byte[] { 0x12, 0x34, 0x56, 0xd2 }) },
            { @$"{root}sub{s}sub{s}image.gif", new MockFileData(new byte[] { 0x12, 0x34, 0x56, 0xd2 }) },
            { @$"{root}sub{s}sub{s}sub{s}image.gif", new MockFileData(new byte[] { 0x12, 0x34, 0x56, 0xd2 }) },
        });

        var detector = new GitRepositoryDetector(fileSystem);
        var cd = @$"{root}sub{s}sub{s}sub{s}";
        var isRepo = detector.DetectGitRepository(cd);

        isRepo.ShouldBeFalse("Should not be a git repository");
    }
}
