using System.Runtime.InteropServices;

namespace Xenial.BTool.Tests;

public sealed class IgnoreUnixFact : FactAttribute
{
    public IgnoreUnixFact()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            Skip = "Ignore on Unix";
        }
    }
}