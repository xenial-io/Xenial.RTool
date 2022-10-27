var sln = "./Xenial.BTool.sln";

Target("restore", () => RunAsync("dotnet", $"restore {sln}"));

Target("build", DependsOn("restore"), 
    () => RunAsync("dotnet", $"build {sln} --no-restore -c Release")
);

Target("test", DependsOn("build"), 
    () => RunAsync("dotnet", $"test {sln} --no-build --no-restore -c Release --logger:\"console;verbosity=normal\"")
);

Target("pack",DependsOn("test"),  
    () => RunAsync("dotnet", $"pack {sln} --no-build --no-restore -c Release")
);

Target("deploy", async () =>
{
    var files = Directory.EnumerateFiles("artifacts/nuget", "*.nupkg");

    foreach (var file in files)
    {
        await RunAsync("dotnet", $"nuget push {file} --skip-duplicate -s https://api.nuget.org/v3/index.json -k {Environment.GetEnvironmentVariable("NUGET_AUTH_TOKEN")}",
            noEcho: true
        );
    }
});

Target("release", () => ReleaseHelper.Release());

Target("default", DependsOn("test"));

await RunTargetsAndExitAsync(args);