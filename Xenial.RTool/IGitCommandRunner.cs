namespace Xenial.RTool;

public interface IGitCommandRunner
{
    Task<string> ReadCommand(string command, string? cd = null);
    Task RunCommand(string command, string? cd = null);
}
