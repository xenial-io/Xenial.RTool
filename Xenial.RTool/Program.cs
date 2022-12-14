using System.IO.Abstractions;

using Spectre.Console;

using Xenial.RTool;

var app = new ReleaseApplication(
    new FileSystem(), 
    AnsiConsole.Console, 
    new GitCommandRunner(),
    new HookCommandRunner()
);

await app.Release();