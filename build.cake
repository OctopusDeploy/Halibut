//////////////////////////////////////////////////////////////////////
// TOOLS
//////////////////////////////////////////////////////////////////////
#tool "nuget:?package=GitVersion.CommandLine&version=4.0.0-beta0007"
#addin "MagicChunks"

//////////////////////////////////////////////////////////////////////
// ARGUMENTS
//////////////////////////////////////////////////////////////////////
var target = Argument("target", "Default");
var configuration = Argument("configuration", "Release");
var forceCiBuild = Argument("forceCiBuild", false);

///////////////////////////////////////////////////////////////////////////////
// GLOBAL VARIABLES
///////////////////////////////////////////////////////////////////////////////
var artifactsDir = "./artifacts/";
var globalAssemblyFile = "./source/Halibut/Properties/AssemblyInfo.cs";
var projectToPackage = "./source/Halibut";

var isContinuousIntegrationBuild = !BuildSystem.IsLocalBuild || forceCiBuild;

var gitVersionInfo = GitVersion(new GitVersionSettings {
    OutputType = GitVersionOutput.Json
});

var nugetVersion = isContinuousIntegrationBuild ? gitVersionInfo.NuGetVersion : "0.0.0";

///////////////////////////////////////////////////////////////////////////////
// SETUP / TEARDOWN
///////////////////////////////////////////////////////////////////////////////
Setup(context =>
{
    Information("Building Halibut v{0}", nugetVersion);
});

Teardown(context =>
{
    Information("Finished running tasks.");
});

//////////////////////////////////////////////////////////////////////
//  PRIVATE TASKS
//////////////////////////////////////////////////////////////////////

Task("__Default")
    .IsDependentOn("__Clean")
    .IsDependentOn("__Restore")
    .IsDependentOn("__UpdateAssemblyVersionInformation")
    .IsDependentOn("__Build")
    .IsDependentOn("__Test")
    .IsDependentOn("__UpdateProjectJsonVersion")
    .IsDependentOn("__Pack");
    // .IsDependentOn("__Publish");

Task("__Clean")
    .Does(() =>
{
    CleanDirectory(artifactsDir);
    CleanDirectories("./src/**/bin");
    CleanDirectories("./src/**/obj");
});

Task("__Restore")
    .Does(() => DotNetCoreRestore());

Task("__UpdateAssemblyVersionInformation")
    .WithCriteria(isContinuousIntegrationBuild)
    .Does(() =>
{
     GitVersion(new GitVersionSettings {
        UpdateAssemblyInfo = true,
        UpdateAssemblyInfoFilePath = globalAssemblyFile
    });

    Information("AssemblyVersion -> {0}", gitVersionInfo.AssemblySemVer);
    Information("AssemblyFileVersion -> {0}", $"{gitVersionInfo.MajorMinorPatch}.0");
    Information("AssemblyInformationalVersion -> {0}", gitVersionInfo.InformationalVersion);
});

Task("__Build")
    .Does(() =>
{
    DotNetCoreBuild("**/project.json", new DotNetCoreBuildSettings
    {
        Configuration = configuration
    });
});

Task("__Test")
    .Does(() =>
{
    DotNetCoreTest("./source/Halibut.Tests", new DotNetCoreTestSettings
    {
        Configuration = configuration,
        Framework = "net451"
    });

    MoveFile("./TestResult.xml", "./TestResult.net451.xml");

    DotNetCoreTest("./source/Halibut.Tests", new DotNetCoreTestSettings
    {
        Configuration = configuration,
        Framework = "netcoreapp1.0"
    });

    MoveFile("./TestResult.xml", "./TestResult.netcoreapp1.0.xml");
});

Task("__UpdateProjectJsonVersion")
    .WithCriteria(isContinuousIntegrationBuild)
    .Does(() =>
{
    var projectToPackagePackageJson = $"{projectToPackage}/project.json";
    Information("Updating {0} version -> {1}", projectToPackagePackageJson, nugetVersion);

    TransformConfig(projectToPackagePackageJson, projectToPackagePackageJson, new TransformationCollection {
        { "version", nugetVersion }
    });
});

Task("__Pack")
    .Does(() =>
{
    DotNetCorePack(projectToPackage, new DotNetCorePackSettings
    {
        Configuration = configuration,
        OutputDirectory = artifactsDir,
        NoBuild = true
    });

    DeleteFiles(artifactsDir + "*symbols*");
});

// Task("__Publish")
//     .WithCriteria(isContinuousIntegrationBuild && !forceCiBuild)
//     .Does(() =>
// {
//     var isPullRequest = !String.IsNullOrEmpty(EnvironmentVariable("APPVEYOR_PULL_REQUEST_NUMBER"));
//     var isMasterBranch = EnvironmentVariable("APPVEYOR_REPO_BRANCH") == "master" && !isPullRequest;
//     var shouldPushToMyGet = !BuildSystem.IsLocalBuild;
//     var shouldPushToNuGet = !BuildSystem.IsLocalBuild && isMasterBranch;

//     if (shouldPushToMyGet)
//     {
//         NuGetPush(artifactsDir + "Halibut." + nugetVersion + ".nupkg", new NuGetPushSettings {
//             Source = "https://octopus.myget.org/F/octopus-dependencies/api/v3/index.json",
//             ApiKey = EnvironmentVariable("MyGetApiKey")
//         });
//         NuGetPush(artifactsDir + "Halibut." + nugetVersion + ".symbols.nupkg", new NuGetPushSettings {
//             Source = "https://octopus.myget.org/F/octopus-dependencies/api/v3/index.json",
//             ApiKey = EnvironmentVariable("MyGetApiKey")
//         });
//     }
//     if (shouldPushToNuGet)
//     {
//         NuGetPush(artifactsDir + "Halibut." + nugetVersion + ".nupkg", new NuGetPushSettings {
//             Source = "https://www.nuget.org/api/v2/package",
//             ApiKey = EnvironmentVariable("NuGetApiKey")
//         });
//         NuGetPush(artifactsDir + "Halibut." + nugetVersion + ".symbols.nupkg", new NuGetPushSettings {
//             Source = "https://www.nuget.org/api/v2/package",
//             ApiKey = EnvironmentVariable("NuGetApiKey")
//         });
//     }
// });

//////////////////////////////////////////////////////////////////////
// TASKS
//////////////////////////////////////////////////////////////////////
Task("GitVersion")
    .Does(() =>
{
     GitVersion(new GitVersionSettings {
        UpdateAssemblyInfo = false,
        UpdateAssemblyInfoFilePath = globalAssemblyFile
    });

    Information("AssemblyVersion -> {0}", gitVersionInfo.AssemblySemVer);
    Information("AssemblyFileVersion -> {0}", $"{gitVersionInfo.MajorMinorPatch}.0");
    Information("AssemblyInformationalVersion -> {0}", gitVersionInfo.InformationalVersion);
    Information("FullSemVer -> {0}", gitVersionInfo.FullSemVer);
});

Task("Default")
    .IsDependentOn("__Default");

Task("Clean")
    .IsDependentOn("__Clean");

Task("Restore")
    .IsDependentOn("__Restore");

Task("Build")
    .IsDependentOn("__Build");

Task("Test")
    .IsDependentOn("__Test");

Task("Pack")
    .IsDependentOn("__Clean")
    .IsDependentOn("__Restore")
    .IsDependentOn("__UpdateProjectJsonVersion")
    .IsDependentOn("__UpdateAssemblyVersionInformation")
    .IsDependentOn("__Build")
    .IsDependentOn("__Pack");

// Task("Publish")
//     .IsDependentOn("Pack")
//     .IsDependentOn("__Publish");

//////////////////////////////////////////////////////////////////////
// EXECUTION
//////////////////////////////////////////////////////////////////////
RunTarget(target);