using System.IO.Abstractions;

using Spectre.Console;

using Xenial.RTool.Common;

namespace Xenial.RTool;

public sealed record ReleaseApplication(
    IFileSystem FileSystem,
    IAnsiConsole AnsiConsole,
    IGitCommandRunner CommandRunner
)
{
    public string CurrentDirectory { get; init; } = Environment.CurrentDirectory;
    public Task Release()
    {
        var middleware = new ReleaseMiddleware();
        middleware
            .Use(next => context => CheckForGit(next, context, new GitRepositoryDetector(FileSystem)));


        var app = middleware.Build();

        var context = new ReleaseContext(FileSystem, AnsiConsole, CommandRunner, CurrentDirectory);

        return app(context);
    }

    public static async Task CheckForGit(ReleaseApplicationDelegate next, ReleaseContext ctx, IGitRepositoryDetector detector)
    {
        if (!detector.DetectGitRepository(ctx.CurrentDirectory))
        {
            ctx.Console.MarkupLineInterpolated($"[red]Not in a git repository: {ctx.CurrentDirectory}[/]");
            return;
        }

        await next(ctx);
    }
}

public sealed record ReleaseMiddleware : MiddlewareBuilder<ReleaseApplicationDelegate, ReleaseMiddleware>
{
    public ReleaseMiddleware() : base(_ => Task.CompletedTask)
    {
    }
}

public sealed record ReleaseContext(
    IFileSystem FileSystem,
    IAnsiConsole Console,
    IGitCommandRunner CommandRunner,
    string CurrentDirectory
);

public delegate Task ReleaseApplicationDelegate(ReleaseContext Context);
