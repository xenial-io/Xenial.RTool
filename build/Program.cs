var sln = "./Xenial.RTool.sln";

Target("restore", () => RunAsync("dotnet", $"restore {sln}"));

Target("build", DependsOn("restore"),
    () => RunAsync("dotnet", $"build {sln} --no-restore -c Release")
);

Target("test", DependsOn("build"),
    () => RunAsync("dotnet", $"test {sln} --no-build --no-restore -c Release --logger:\"console;verbosity=normal\"")
);

Target("pack", DependsOn("test"),
    () => RunAsync("dotnet", $"pack {sln} --no-build --no-restore -c Release")
);

Target("poke:version", DependsOn("test"), async () =>
    {
        var json = await File.ReadAllTextAsync("package.json");
        json = PokeJson.AddOrUpdateJsonValue(json, "version", Environment.GetEnvironmentVariable("SEMVER"));
        await File.WriteAllTextAsync("package.json", json);
    }
);

Target("commit:version", async () =>
{
    await RunAsync("git", $"add .");
    await RunAsync("git", $"commit -m \"release: new version {Environment.GetEnvironmentVariable("SEMVER")}\"");
}
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

Target("default", DependsOn("test"));

await RunTargetsAndExitAsync(args);