using static SimpleExec.Command;

namespace Xenial.RTool;

public sealed class HookCommandRunner : IHookCommandRunner
{
    public Task RunCommand(string command, string? args, string? cd = null, Dictionary<string, string>? envVars = null)
        => RunAsync(
            command,
            args ?? "",
            workingDirectory: cd ?? Environment.CurrentDirectory,
            handleExitCode: exitCode =>
            {
                if (exitCode == 0)
                {
                    return true;
                }
                return false;
            }, configureEnvironment: args =>
            {
                foreach (var pair in envVars ?? new())
                {
                    args[pair.Key] = pair.Value;
                }
            }
        );

}
