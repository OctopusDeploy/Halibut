// ReSharper disable RedundantUsingDirective
using System;
using JetBrains.Annotations;
using Nuke.Common;
using Nuke.Common.CI;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.OctoVersion;
using Nuke.Common.Utilities;
using Nuke.Common.Utilities.Collections;
using static Nuke.Common.IO.FileSystemTasks;
using static Nuke.Common.Tools.DotMemoryUnit.DotMemoryUnitTasks;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

[ShutdownDotNetAfterServerBuild]
class Build : NukeBuild
{
    /// Support plugins are available for:
    ///   - JetBrains ReSharper        https://nuke.build/resharper
    ///   - JetBrains Rider            https://nuke.build/rider
    ///   - Microsoft VisualStudio     https://nuke.build/visualstudio
    ///   - Microsoft VSCode           https://nuke.build/vscode

    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
    readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;

    [Solution(GenerateProjects = true)] 
    readonly Solution Solution;

    [Parameter("Whether to auto-detect the branch name - this is okay for a local build, but should not be used under CI.")]
    readonly bool AutoDetectBranch = IsLocalBuild;

    [Parameter("Branch name for OctoVersion to use to calculate the version number. Can be set via the environment variable OCTOVERSION_CurrentBranch.", Name = "OCTOVERSION_CurrentBranch")]
    readonly string BranchName;

    [OctoVersion(UpdateBuildNumber = true, BranchMember = nameof(BranchName), AutoDetectBranchMember = nameof(AutoDetectBranch), Framework = "net6.0")]
    readonly OctoVersionInfo OctoVersionInfo;

    static AbsolutePath SourceDirectory => RootDirectory / "source";
    static AbsolutePath ArtifactsDirectory => RootDirectory / "artifacts";
    static AbsolutePath LocalPackagesDirectory => RootDirectory / ".." / "LocalPackages";

    Target Clean => _ => _
        .Executes(() =>
        {
            SourceDirectory.GlobDirectories("**/bin", "**/obj").ForEach(DeleteDirectory);
            EnsureCleanDirectory(ArtifactsDirectory);
        });

    Target Restore => _ => _
        .DependsOn(Clean)
        .Executes(() =>
        {
            DotNetRestore(s => s
                .SetProjectFile(Solution));


        });

    Target Compile => _ => CompileDefinition(_, null);

    Target CompileNet48 => _ => CompileDefinition(_, "net48");

    Target CompileNet60 => _ => CompileDefinition(_, "net6.0");

    ITargetDefinition CompileDefinition(ITargetDefinition targetDefinition, [CanBeNull] string framework)
    {
        return targetDefinition
            .DependsOn(Restore)
            .Executes(() =>
            {
                DotNetBuild(s => s
                    .SetProjectFile(Solution)
                    .SetConfiguration(Configuration)
                    .SetFramework(framework)
                    .SetVersion(OctoVersionInfo.FullSemVer)
                    .SetInformationalVersion(OctoVersionInfo.InformationalVersion)
                    .EnableNoRestore());
            });
    }

    [PublicAPI]
    Target TestWindows => _ => TestDefinition(_, Compile, null, runDotMemoryTests:true);

    [PublicAPI]
    Target TestLinux => _ => TestDefinition(_, Compile, null, runDotMemoryTests: false);

    [PublicAPI]
    Target TestWindowsNet48 => _ => TestDefinition(_, CompileNet48, "net48", runDotMemoryTests: true);

    [PublicAPI]
    Target TestWindowsNet60 => _ => TestDefinition(_, CompileNet60, "net6.0", runDotMemoryTests: true);

    ITargetDefinition TestDefinition(ITargetDefinition targetDefinition, Target dependsOn, [CanBeNull] string framework, bool runDotMemoryTests)
    {
        return targetDefinition
            .DependsOn(dependsOn)
            .Executes(() =>
            {
                if (runDotMemoryTests)
                {
                    var frameworkOption = framework != null ? $"--framework={framework}" : "";

                    DotMemoryUnit($"{DotNetPath.DoubleQuoteIfNeeded()} --propagate-exit-code --instance-name={Guid.NewGuid()} -- test {Solution.Halibut_Tests_DotMemory.Path} --configuration={Configuration} {frameworkOption} --no-build");
                }
                
                DotNetTest(_ => _
                    .SetProjectFile(Solution.Halibut_Tests)
                    .SetConfiguration(Configuration)
                    .SetFramework(framework)
                    .EnableNoBuild()
                    .EnableNoRestore()
                    .EnableBlameCrash()
                    .EnableBlameCrashCollectAlways() // Lets make sure we can collect them.
                    .SetBlameCrashDumpType("full")
                    .EnableBlameHang()
                    .SetBlameHangTimeout(20)
                    .SetBlameHangDumpType("full"));

            });
    }

    Target Pack => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            DotNetPack(_ => _
                .SetProject(Solution.Halibut)
                .SetVersion(OctoVersionInfo.FullSemVer)
                .SetConfiguration(Configuration)
                .SetOutputDirectory(ArtifactsDirectory)
                .EnableNoBuild()
                .DisableIncludeSymbols()
                .SetVerbosity(DotNetVerbosity.Normal));
        });

    Target CopyToLocalPackages => _ => _
        .OnlyWhenStatic(() => IsLocalBuild)
        .TriggeredBy(Pack)
        .Executes(() =>
        {
            EnsureExistingDirectory(LocalPackagesDirectory);
            ArtifactsDirectory.GlobFiles("*.nupkg")
                .ForEach(package => CopyFileToDirectory(package, LocalPackagesDirectory, FileExistsPolicy.Overwrite));
        });
    
    [PublicAPI]
    Target PackTestPortForwarder => _ => _
        .DependsOn(Clean)
        .Executes(() =>
        {
            DotNetPack(_ => _
                .SetProject(Solution.Octopus_TestPortForwarder)
                .SetVersion(OctoVersionInfo.FullSemVer)
                .SetConfiguration(Configuration)
                .SetOutputDirectory(ArtifactsDirectory)
                .SetVerbosity(DotNetVerbosity.Normal));
        });

    Target Default => _ => _
        .DependsOn(Pack)
        .DependsOn(CopyToLocalPackages)
        .DependsOn(PackTestPortForwarder);

    public static int Main () => Execute<Build>(x => x.Default);
}
