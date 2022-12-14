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
    private IHookCommandRunner HookCommandRunner { get; } = A.Fake<IHookCommandRunner>();

    private ReleaseContext CreateTestContext(string? cd = null)
        => new ReleaseContext(
            FileSystem,
            Console,
            CommandRunner,
            HookCommandRunner,
            cd ?? Environment.CurrentDirectory
    );

    private (ReleaseApplication app, ReleaseContext ctx) CreateApplication()
        => (new ReleaseApplication(
                FileSystem, 
                Console, 
                CommandRunner, 
                HookCommandRunner
            ), CreateTestContext());

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


    [Fact]
    public async Task RunPreReleaseHooksMustCallNextWhenInGitRepo()
    {
        var next = A.Fake<ReleaseApplicationDelegate>();
        var detector = A.Fake<IGitRepositoryRootDetector>();
        var runner = A.Fake<IHookCommandRunner>();
        A.CallTo(() => detector.DetectGitRootDirectory(A<string>.Ignored)).Returns("var/www/myproject");

        var (app, ctx) = CreateApplication();

        await app.RunPreReleaseHooks(next, ctx, detector);

        A.CallTo(next).MustHaveHappened();
    }

    [Fact]
    public async Task RunPreReleaseHooks()
    {
        var detector = A.Fake<IGitRepositoryRootDetector>();
        A.CallTo(() => detector.DetectGitRootDirectory(A<string>.Ignored)).Returns("var/www/myproject");
        FileSystem.AddFile("var/www/myproject/.r-tool.json", new MockFileData("""
            {
                "hooks": {
                    "pre": [
                        { "command": "foo", "args": "bar baz" }
                    ]
                }
            }
            """));

        var next = A.Fake<ReleaseApplicationDelegate>(); 
        var (app, ctx) = CreateApplication();

        await app.RunPreReleaseHooks(next, ctx, detector);

        A.CallTo(
            () => HookCommandRunner.RunCommand("foo", "bar baz", ctx.CurrentDirectory, A<Dictionary<string, string>>.Ignored)
        ).MustHaveHappened();
    }
}
