using System.IO.Abstractions;
using System.Text.Json;
using System.Text.Json.Serialization;

using Spectre.Console;

using Xenial.RTool.Common;

namespace Xenial.RTool;

public sealed record ReleaseApplication(
    IFileSystem FileSystem,
    IAnsiConsole AnsiConsole,
    IGitCommandRunner CommandRunner,
    IHookCommandRunner HookCommandRunner
)
{
    public enum VersionIncrement
    {
        Patch,
        Minor,
        Major
    }

    public string CurrentDirectory { get; init; } = Environment.CurrentDirectory;
    public Task Release()
    {
        var middleware = new ReleaseMiddleware();

        middleware
            .Use(next => ctx => HandleErrors(next, ctx))
            .Use(next => ctx => CheckForGit(next, ctx, new GitRepositoryDetector(FileSystem)))
            .Use(next => ctx => ConfirmBranch(next, ctx))
            .Use(next => ctx => PullChanges(next, ctx))
            .Use(next => ctx => FetchTags(next, ctx))
            .Use(next => ctx => ListTags(next, ctx))
            .Use(next => ctx => ParseVersions(next, ctx))
            .Use(next => ctx => FindMaxVersion(next, ctx))
            .Use(next => ctx => AskVersion(next, ctx))
            .Use(next => ctx => ConfirmVersion(next, ctx))
            .Use(next => ctx => RunPreReleaseHooks(next, ctx, new GitRepositoryDetector(FileSystem)))
            .Use(next => ctx => TagVersion(next, ctx))
            .Use(next => ctx => PushTags(next, ctx))
        ;

        var app = middleware.Build();

        var context = new ReleaseContext(
            FileSystem,
            AnsiConsole,
            CommandRunner,
            HookCommandRunner,
            CurrentDirectory);

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

    public async Task HandleErrors(ReleaseApplicationDelegate next, ReleaseContext ctx)
    {
        try
        {
            await next(ctx);
        }
        catch (Exception e)
        {
            ctx.Console.WriteException(e);
        }
    }

    public async Task ConfirmBranch(ReleaseApplicationDelegate next, ReleaseContext ctx)
    {
        var currentBranch = await ctx.CommandRunner.ReadCommand("branch --show-current", ctx.CurrentDirectory);

        if (!mainBranches.Contains(currentBranch))
        {
            var branchStrings = string.Join(" or ", mainBranches);
            ctx.Console.MarkupLineInterpolated($"[yellow]The current branch you are working on is not [/][white]{branchStrings}[/]");
            ctx.Console.MarkupLineInterpolated($"[yellow]actually it is [red]{currentBranch}[/][/]");

            var result = ctx.Console.Prompt(
                new ConfirmationPrompt("Do you want to continue?") { DefaultValue = false }
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

    public async Task PullChanges(ReleaseApplicationDelegate next, ReleaseContext ctx)
    {
        ctx.Console.MarkupLineInterpolated($"[gray]Fetching latest changes from remote...[/]");
        await ctx.CommandRunner.RunCommand("pull");
        await next(ctx);
    }

    public async Task FetchTags(ReleaseApplicationDelegate next, ReleaseContext ctx)
    {
        ctx.Console.MarkupLineInterpolated($"[gray]Fetching latest tags from remote...[/]");
        await ctx.CommandRunner.RunCommand("pull --tags");
        await next(ctx);
    }

    public async Task ListTags(ReleaseApplicationDelegate next, ReleaseContext ctx)
    {
        var tagResult = await ctx.CommandRunner.ReadCommand("tag", ctx.CurrentDirectory);

        if (!string.IsNullOrEmpty(tagResult))
        {
            var tags = tagResult.Split("\n");
            ctx.Tags.AddRange(tags);
            if (ctx.Tags.Count > 0)
            {
                ctx.Console.MarkupLineInterpolated($"[grey]Current Tags:{Environment.NewLine} {string.Join($"{Environment.NewLine} ", tags)}[/]");
            }
        }

        await next(ctx);
    }


    public async Task ParseVersions(ReleaseApplicationDelegate next, ReleaseContext ctx)
    {
        IEnumerable<string> CollectVersion(char versionSelector)
            => ctx.Tags.Where(v => v.StartsWith(versionSelector)).Select(v => v.TrimStart(versionSelector));

        var versionsWithV = CollectVersion('v').Concat(CollectVersion('V'));

        ctx.Versions = versionsWithV
            .Select(v =>
            {
                if (Version.TryParse(v, out var version))
                {
                    return version;
                }
                return null;
            })
            .OfType<Version>()
            .OrderBy(r => r)
            .ToList();

        if (ctx.Versions.Count > 0)
        {
            ctx.Console.MarkupLineInterpolated($"[grey]Current Versions:{Environment.NewLine} {string.Join($"{Environment.NewLine} ", ctx.Versions)}[/]");
        }

        await next(ctx);
    }

    public async Task FindMaxVersion(ReleaseApplicationDelegate next, ReleaseContext ctx)
    {
        var version = ctx.Versions.Max();

        if (version is null)
        {
            ctx.Console.MarkupLine("[yellow]There is currently no version yet.[/] [grey]Assuming you want to release [white]0.0.1[/][/]");
            ctx.CurrentVersion = new Version(0, 0, 0);
        }
        else
        {
            ctx.CurrentVersion = version;
            ctx.Console.MarkupLineInterpolated($"[gray]The current version is [white]{ctx.CurrentVersion}[/][/]");
        }

        await next(ctx);
    }

    public async Task AskVersion(ReleaseApplicationDelegate next, ReleaseContext ctx)
    {
        var increment = ctx.Console.Prompt(
            new SelectionPrompt<VersionIncrement>()
                .Title("Which increment would you like to make?")
                .AddChoices(
                    Enum.GetValues(typeof(VersionIncrement))
                        .OfType<VersionIncrement>()
                )
                .UseConverter(v => $"{v} - {NextVersion(ctx.CurrentVersion, v)}")
        );

        ctx.NewVersion = NextVersion(ctx.CurrentVersion, increment);

        ctx.Console.MarkupLineInterpolated($"[gray]The next version will be [green]{ctx.NewVersion}[/][/]");

        await next(ctx);
    }

    private static Version NextVersion(Version version, VersionIncrement increment)
        => increment switch
        {
            VersionIncrement.Major => new Version(version.Major + 1, 0, 0),
            VersionIncrement.Minor => new Version(version.Major, version.Minor + 1, 0),
            VersionIncrement.Patch => new Version(version.Major, version.Minor, version.Build + 1),
            _ => version
        };

    public async Task ConfirmVersion(ReleaseApplicationDelegate next, ReleaseContext ctx)
    {
        var result = ctx.Console.Prompt(
            new ConfirmationPrompt("Do you want to continue?")
        );

        if (result)
        {
            await next(ctx);
        }
    }

    public async Task TagVersion(ReleaseApplicationDelegate next, ReleaseContext ctx)
    {
        var tagName = $"v{ToSemVer(ctx.NewVersion)}";

        ctx.Console.MarkupLineInterpolated($"[gray]Tagging: [white]{tagName}[/][/]");
        await ctx.CommandRunner.RunCommand($"tag {tagName}", ctx.CurrentDirectory);

        await next(ctx);
    }

    private static string ToSemVer(Version version)
        => $"{version.Major}.{version.Minor}.{version.Build}";

    public async Task PushTags(ReleaseApplicationDelegate next, ReleaseContext ctx)
    {
        ctx.Console.MarkupLineInterpolated($"[gray]Pushing tags to remote...[/]");
        await ctx.CommandRunner.RunCommand($"push --tags", ctx.CurrentDirectory);

        await next(ctx);
    }
    public async Task RunPreReleaseHooks(
        ReleaseApplicationDelegate next,
        ReleaseContext ctx,
        IGitRepositoryRootDetector rootDetector
    )
    {
        var root = rootDetector.DetectGitRootDirectory(ctx.CurrentDirectory);
        if (!string.IsNullOrEmpty(root))
        {
            var file = ctx.FileSystem.Path.Combine(root, ".r-tool.json");
            if (ctx.FileSystem.File.Exists(file))
            {
                var jsonContent = await ctx.FileSystem.File.ReadAllTextAsync(file);
                var config = JsonSerializer.Deserialize<RToolJson>(jsonContent);
                if (config is not null && config.Hooks is not null && config.Hooks.Pre is not null)
                {
                    var semver = ToSemVer(ctx.NewVersion);
                    var tagName = $"v{semver}";

                    foreach (var hook in config.Hooks.Pre)
                    {
                        if (!string.IsNullOrEmpty(hook.Command))
                        {
                            ctx.Console.MarkupLineInterpolated($"[gray]Running Hook: [white]{hook}[/]...[/]");
                            await ctx.HookCommandRunner.RunCommand(
                                hook.Command,
                                hook.Args,
                                hook.WorkingDirectory ?? ctx.CurrentDirectory,
                                new()
                                {
                                    ["SEMVER"] = semver,
                                    ["TAGNAME"] = tagName
                                }
                            );
                            ctx.Console.MarkupLineInterpolated($"[gray]Ran Hook: [white]{hook}[/].[/]");
                        }
                    }
                }
            }
            await next(ctx);
        }
    }
}

public sealed record RToolJson
{
    [JsonPropertyName("hooks")]
    public RToolJsonHooks? Hooks { get; set; }
}

public sealed record RToolJsonHooks
{
    [JsonPropertyName("pre")]
    public RToolJsonHook[]? Pre { get; init; }
}

public sealed record RToolJsonHook
{
    [JsonPropertyName("command")]
    public string? Command { get; set; }
    [JsonPropertyName("args")]
    public string? Args { get; set; }
    [JsonPropertyName("wd")]
    public string? WorkingDirectory { get; set; }
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
    IHookCommandRunner HookCommandRunner,
    string CurrentDirectory
)
{
    public List<string> Tags { get; set; } = new();
    public List<Version> Versions { get; set; } = new();
    public Version CurrentVersion { get; set; } = new();
    public Version NewVersion { get; set; } = new();
};

public delegate Task ReleaseApplicationDelegate(ReleaseContext Context);
