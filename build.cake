//////////////////////////////////////////////////////////////////////
// TOOLS
//////////////////////////////////////////////////////////////////////
#tool "nuget:?package=GitVersion.CommandLine&version=4.0.0"
#tool "nuget:?package=gitlink&version=3.1.0"
#tool "nuget:?package=JetBrains.DotMemoryUnit&version=3.0.20171219.105559"
#addin "Cake.FileHelpers&version=3.2.0"

//////////////////////////////////////////////////////////////////////
// ARGUMENTS
//////////////////////////////////////////////////////////////////////
var target = Argument("target", "Default");
var configuration = Argument("configuration", "Release");

///////////////////////////////////////////////////////////////////////////////
// GLOBAL VARIABLES
///////////////////////////////////////////////////////////////////////////////
var artifactsDir = "./artifacts/";
var localPackagesDir = "../LocalPackages";

GitVersion gitVersionInfo;
string nugetVersion;


///////////////////////////////////////////////////////////////////////////////
// SETUP / TEARDOWN
///////////////////////////////////////////////////////////////////////////////
Setup(context =>
{
    gitVersionInfo = GitVersion(new GitVersionSettings {
        OutputType = GitVersionOutput.Json
    });

    if(BuildSystem.IsRunningOnTeamCity)
        BuildSystem.TeamCity.SetBuildNumber(gitVersionInfo.NuGetVersion);

    nugetVersion = gitVersionInfo.NuGetVersion;

    Information("Building Halibut v{0}", nugetVersion);
    Information("Informational Version {0}", gitVersionInfo.InformationalVersion);
});

Teardown(context =>
{
    Information("Finished running tasks.");
});

//////////////////////////////////////////////////////////////////////
//  PRIVATE TASKS
//////////////////////////////////////////////////////////////////////

Task("Clean")
    .Does(() =>
{
    CleanDirectory(artifactsDir);
    CleanDirectories("./source/**/bin");
    CleanDirectories("./source/**/obj");
    CleanDirectories("./source/**/TestResults");
});

Task("Restore")
    .IsDependentOn("Clean")
    .Does(() => {
        DotNetCoreRestore("source");
    });


Task("Build")
    .IsDependentOn("Restore")
    .IsDependentOn("Clean")
    .Does(() =>
{
    DotNetCoreBuild("./source", new DotNetCoreBuildSettings
    {
        Configuration = configuration,
        ArgumentCustomization = args => args.Append($"/p:Version={nugetVersion}")
    });
});

Task("Test")
    .IsDependentOn("Build")
    .Does(() =>
{
    DotNetCoreTest("./source/Halibut.Tests/Halibut.Tests.csproj", new DotNetCoreTestSettings
    {
        ArgumentCustomization = args => {
            args.Clear();
            args.Append("\"C:/Program Files/dotnet/dotnet.exe\"");
            args.Append("--propagate-exit-code");
            args.Append("--instance-name=" + Guid.NewGuid());
            args.Append("--");
            args.Append("test");
            args.Append("./source/Halibut.Tests/Halibut.Tests.csproj");
            args.Append("--configuration=" + configuration);
            args.Append("--no-build");
            return args;
        },
        ToolPath = "./tools/JetBrains.dotMemoryUnit.3.0.20171219.105559/lib/tools/dotMemoryUnit.exe"
    });
});

Task("PublishLinuxTests")
    .IsDependentOn("Test")
    .Does(() =>
{
    DotNetCorePublish("./source/Halibut.Tests/Halibut.Tests.csproj", new DotNetCorePublishSettings
    {
        Configuration = configuration,
        Framework = "netcoreapp2.2",
        Runtime = "linux-x64",
        OutputDirectory = new DirectoryPath($"{artifactsDir}publish/linux-x64")
    });
});

Task("Pack")
    .IsDependentOn("PublishLinuxTests")
    .Does(() =>
{
    var pdbs = GetFiles($"./source/Halibut/bin/{configuration}/**/Halibut.pdb");
    foreach(var pdb in pdbs)
    {
        GitLink3(pdb);
    }

    DotNetCorePack("./source/Halibut", new DotNetCorePackSettings
    {
        Configuration = configuration,
        OutputDirectory = artifactsDir,
        NoBuild = true,
        IncludeSource = false,
        ArgumentCustomization = args => args.Append($"/p:Version={nugetVersion}")
    });

    DeleteFiles(artifactsDir + "*symbols*");
});

Task("CopyToLocalPackages")
    .IsDependentOn("Pack")
    .WithCriteria(BuildSystem.IsLocalBuild)
    .Does(() =>
{
    CreateDirectory(localPackagesDir);
    CopyFileToDirectory($"{artifactsDir}/Halibut.{nugetVersion}.nupkg", localPackagesDir);
});

Task("Publish")
    .IsDependentOn("Pack")
    .WithCriteria(BuildSystem.IsRunningOnTeamCity)
    .Does(() =>
{
	NuGetPush($"{artifactsDir}Halibut.{nugetVersion}.nupkg", new NuGetPushSettings {
		Source = "https://f.feedz.io/octopus-deploy/dependencies/nuget",
		ApiKey = EnvironmentVariable("FeedzIoApiKey")
	});

    if (gitVersionInfo.PreReleaseTagWithDash == "")
    {
          NuGetPush($"{artifactsDir}Halibut.{nugetVersion}.nupkg", new NuGetPushSettings {
            Source = "https://www.nuget.org/api/v2/package",
            ApiKey = EnvironmentVariable("NuGetApiKey")
        });
    }
});

Task("Default")
    .IsDependentOn("CopyToLocalPackages")
    .IsDependentOn("Publish");

//////////////////////////////////////////////////////////////////////
// EXECUTION
//////////////////////////////////////////////////////////////////////
RunTarget(target);
