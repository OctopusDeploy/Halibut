using System;
using JetBrains.Annotations;
using Nuke.Common;
using Nuke.Common.CI;
using Nuke.Common.Execution;
using Nuke.Common.Git;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.GitLink;
using Nuke.Common.Tools.GitVersion;
using Nuke.Common.Utilities.Collections;
using static Nuke.Common.IO.FileSystemTasks;
using static Nuke.Common.Tools.DotMemoryUnit.DotMemoryUnitTasks;
using static Nuke.Common.Tools.DotNet.DotNetTasks;
using static Nuke.Common.Tools.GitLink.GitLinkTasks;

[CheckBuildProjectConfigurations]
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

    [Solution(GenerateProjects = true)] readonly Solution Solution;

    [GitRepository] readonly GitRepository GitRepository;

    [GitVersion] readonly GitVersion GitVersion;
    
    // [PackageExecutable("JetBrains.dotMemoryUnit", "dotMemoryUnit.exe")] readonly Tool DotMemoryUnit;

    static AbsolutePath LocalPackagesDir => RootDirectory / ".." / "LocalPackages";
    
    AbsolutePath SourceDirectory => RootDirectory / "source";

    // AbsolutePath TestsDirectory => SourceDirectory / "Halibut.Tests" / "Halibut.Tests.csproj";

    AbsolutePath ArtifactsDirectory => RootDirectory / "artifacts";

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

    Target Compile => _ => _
        .DependsOn(Restore)
        .Executes(() =>
        {
            DotNetBuild(s => s
                .SetProjectFile(Solution)
                .SetConfiguration(Configuration)
                .SetAssemblyVersion(GitVersion.AssemblySemVer)
                .SetFileVersion(GitVersion.AssemblySemFileVer)
                .SetInformationalVersion(GitVersion.InformationalVersion)
                .EnableNoRestore());
        });

    [PublicAPI]
    Target TestWindows => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            // TODO: Can I toString the dotnet command
            Arguments args = new Arguments()
                .Add($"\"{DotNetPath}\"")
                .Add("--propagate-exit-code")
                .Add("--instance-name=" + Guid.NewGuid())
                .Add("--")
                .Add("test")
                .Add(Solution.Halibut_Tests.Path)
                .Add("--configuration=" + Configuration)
                .Add("--no-build");
            DotMemoryUnit(args.ToString());
        });

    [PublicAPI]
    Target TestLinux => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            DotNetTest(_ => _
                .SetProjectFile(Solution)
                .SetConfiguration(Configuration)
                .EnableNoBuild()
                .EnableNoRestore());
        });

    Target Pack => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            
            var pdbs = SourceDirectory.GlobFiles($"**/bin/{Configuration}/**/Halibut.pdb");
            foreach (var pdb in pdbs)
            {
                var settings = new GitLink3Settings().SetPdbFile(pdb);
                GitLink3(settings);
            }
            
            DotNetPack(_ => _
                .SetProject(Solution.Halibut)
                .SetVersion(GitVersion.FullSemVer)
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
            EnsureExistingDirectory(LocalPackagesDir);
            ArtifactsDirectory.GlobFiles("*.nupkg")
                .ForEach(package => CopyFileToDirectory(package, LocalPackagesDir, FileExistsPolicy.Overwrite));
        });

    Target Default => _ => _
        .DependsOn(Pack)
        .DependsOn(CopyToLocalPackages);

    public static int Main () => Execute<Build>(x => x.Default);
}
