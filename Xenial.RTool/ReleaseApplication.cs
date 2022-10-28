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
            .Use(next => ctx => CheckForGit(next, ctx, new GitRepositoryDetector(FileSystem)))
            .Use(next => ctx => ConfirmBranch(next, ctx));

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

    private static readonly string[] mainBranches = new[]
    {
        "main",
        "master"
    };

    public async Task ConfirmBranch(ReleaseApplicationDelegate next, ReleaseContext ctx)
    {
        var currentBranch = await ctx.CommandRunner.ReadCommand("branch --show-current", ctx.CurrentDirectory);

        if (!mainBranches.Contains(currentBranch))
        {
            var branchStrings = string.Join(" or ", mainBranches);
            ctx.Console.MarkupLineInterpolated($"[yellow]The current branch you are working on is not [/][white]{branchStrings}[/]");
            ctx.Console.MarkupLineInterpolated($"[yellow]actually it is [red]{currentBranch}[/][/]");
            
            var result = ctx.Console.Prompt(
                new ConfirmationPrompt("Do you want to continue") { DefaultValue = false }
            );

            if (result)
            {
                await next(ctx);
            }
        }
        else
        {
            await next(ctx);
        }
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
