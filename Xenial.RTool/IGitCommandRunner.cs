namespace Xenial.RTool;

public interface IGitCommandRunner
{
    Task<string> ReadCommand(string command, string? cd = null);
    Task RunCommand(string command, string? cd = null);
}

public interface IHookCommandRunner
{
    Task RunCommand(string command, string? args, string? cd = null, Dictionary<string, string>? envVars = null);
}