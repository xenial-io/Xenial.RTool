using static SimpleExec.Command;

namespace Xenial.RTool;

public sealed class GitCommandRunner : IGitCommandRunner
{
    public Task RunCommand(string command, string? cd = null)
        => RunAsync("git", command, workingDirectory: cd ?? Environment.CurrentDirectory);

    public async Task<string> ReadCommand(string command, string? cd = null)
    {
        var (stdOut, _) = (await ReadAsync("git", command, workingDirectory: cd ?? Environment.CurrentDirectory));
        var result = stdOut.Trim();
        return result;
    }
}

public sealed class HookCommandRunner : IHookCommandRunner
{
    public Task RunCommand(string command, string args, string? cd = null)
        => RunAsync(command, args, workingDirectory: cd ?? Environment.CurrentDirectory);

    public async Task<string> ReadCommand(string command, string? args, string? cd = null)
    {
        var (stdOut, _) = (await ReadAsync(command, args, workingDirectory: cd ?? Environment.CurrentDirectory));
        var result = stdOut.Trim();
        return result;
    }
}
