using System;
using System.Collections.Generic;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using FakeItEasy;

using Spectre.Console.Testing;

using Xenial.RTool;

namespace Xenial.Tests.RTool;

[UsesVerify]
public sealed class ReleaseApplicationTests
{
    private MockFileSystem FileSystem { get; } = new MockFileSystem(
        new Dictionary<string, MockFileData>()
    );
    private TestConsole Console { get; } = new TestConsole();
    private IGitCommandRunner GitCommandRunner { get; } = A.Fake<IGitCommandRunner>();

    private ReleaseContext CreateTestContext(string? cd = null)
        => new ReleaseContext(
            FileSystem,
            Console,
            GitCommandRunner,
            cd ?? Environment.CurrentDirectory
    );

    [Fact]
    public async Task CheckForGitMustNotCallNextWhenNotInGitRepo()
    {
        var next = A.Fake<ReleaseApplicationDelegate>();
        var detector = A.Fake<IGitRepositoryDetector>();
        A.CallTo(() => detector.DetectGitRepository(A.Dummy<string>())).Returns(false);
        var ctx = CreateTestContext("/var/www/");

        await ReleaseApplication.CheckForGit(next, ctx, detector);

        A.CallTo(next).MustNotHaveHappened();

        await Verify(Console.Output);
    }

    [Fact]
    public async Task CheckForGitMustCallNextWhenInGitRepo()
    {
        var next = A.Fake<ReleaseApplicationDelegate>();
        var detector = A.Fake<IGitRepositoryDetector>();
        A.CallTo(() => detector.DetectGitRepository(A<string>.Ignored)).Returns(true);

        var ctx = CreateTestContext();

        await ReleaseApplication.CheckForGit(next, ctx, detector);

        A.CallTo(next).MustHaveHappened();
    }
}
