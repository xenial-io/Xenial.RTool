using System.IO.Abstractions.TestingHelpers;

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
    private IGitCommandRunner CommandRunner { get; } = A.Fake<IGitCommandRunner>();

    private ReleaseContext CreateTestContext(string? cd = null)
        => new ReleaseContext(
            FileSystem,
            Console,
            CommandRunner,
            cd ?? Environment.CurrentDirectory
    );

    private (ReleaseApplication app, ReleaseContext ctx) CreateApplication()
        => (new ReleaseApplication(FileSystem, Console, CommandRunner), CreateTestContext());

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

    [Theory]
    [InlineData("main", false, true)]
    [InlineData("master", false, true)]
    [InlineData("develop", true, false)]
    [InlineData("topic/my-feature-123", true, true)]
    public async Task ConfirmBranches(string currentBranch, bool mustConfirm, bool confirm)
    {
        var next = A.Fake<ReleaseApplicationDelegate>();

        A.CallTo(() => CommandRunner.ReadCommand("branch --show-current", A<string>.Ignored))
            .Returns(Task.FromResult(currentBranch));

        if (mustConfirm)
        {
            Console.Input.PushTextWithEnter(confirm ? "y" : "n");
        }

        var (app, ctx) = CreateApplication();

        await app.ConfirmBranch(next, ctx);

        if (confirm)
        {
            A.CallTo(next).MustHaveHappened();
        }
        else
        {
            A.CallTo(next).MustNotHaveHappened();
        }

        await Verify(Console.Output)
            .UseParameters(currentBranch, mustConfirm, confirm);
    }
}
