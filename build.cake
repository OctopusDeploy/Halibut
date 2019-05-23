//////////////////////////////////////////////////////////////////////
// TOOLS
//////////////////////////////////////////////////////////////////////
#tool "nuget:?package=GitVersion.CommandLine&version=4.0.0"
#tool "nuget:?package=gitlink&version=3.1.0"
#addin "Cake.FileHelpers&version=3.2.0"

using System.Security.Cryptography.X509Certificates;

//////////////////////////////////////////////////////////////////////
// ARGUMENTS
//////////////////////////////////////////////////////////////////////
var target = Argument("target", "Default");
var configuration = Argument("configuration", "Release");
var signingCertificatePath = Argument("signing_certificate_path", "");
var signingCertificatePassword = Argument("signing_certificate_password", "");

///////////////////////////////////////////////////////////////////////////////
// GLOBAL VARIABLES
///////////////////////////////////////////////////////////////////////////////
var artifactsDir = "./artifacts/";
var localPackagesDir = "../LocalPackages";
var signToolPath = MakeAbsolute(File("./certificates/signtool.exe"));

GitVersion gitVersionInfo;
string nugetVersion;

// From time to time the timestamping services go offline, let's try a few of them so our builds are more resilient
var timestampUrls = new string[]
{
    "http://timestamp.globalsign.com/scripts/timestamp.dll",
    "http://www.startssl.com/timestamp",
    "http://timestamp.comodoca.com/rfc3161", 
    "http://timestamp.verisign.com/scripts/timstamp.dll",
    "http://tsa.starfieldtech.com"
};

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
//  UTILITY FUNCTIONS
//////////////////////////////////////////////////////////////////////
private void SignAndTimestampBinaries(string outputDirectory)
{
    Information($"Signing binaries in {outputDirectory}");
    Information($"{outputDirectory}/Halibut.dll");
    // check that any unsigned libraries, that Octopus Deploy authors, get signed to play nice with security scanning tools
    // refer: https://octopusdeploy.slack.com/archives/C0K9DNQG5/p1551655877004400
    // decision re: no signing everything: https://octopusdeploy.slack.com/archives/C0K9DNQG5/p1557938890227100
     var unsignedExecutablesAndLibraries = 
         GetFiles($"{outputDirectory}/**/Halibut.dll")
         .Where(f => !HasAuthenticodeSignature(f))
         .ToArray();

    Information($"Using signtool in {signToolPath}");
    SignFiles(unsignedExecutablesAndLibraries, signingCertificatePath, signingCertificatePassword);
    TimeStampFiles(unsignedExecutablesAndLibraries);
}
// note: Doesn't check if existing signatures are valid, only that one exists 
// source: https://blogs.msdn.microsoft.com/windowsmobile/2006/05/17/programmatically-checking-the-authenticode-signature-on-a-file/
private bool HasAuthenticodeSignature(FilePath filePath)
{
    try
    {
        X509Certificate.CreateFromSignedFile(filePath.FullPath);
        return true;
    }
    catch
    {
        return false;
    }
}

void SignFiles(IEnumerable<FilePath> files, FilePath certificatePath, string certificatePassword, string display = "", string displayUrl = "")
{
    if (!FileExists(signToolPath))
    {
        throw new Exception($"The signing tool was expected to be at the path '{signToolPath}' but wasn't available.");
    }

    if (!FileExists(certificatePath))
        throw new Exception($"The code-signing certificate was not found at {certificatePath}.");
    
    Information($"Signing {files.Count()} files using certificate at '{certificatePath}'...");

    var signArguments = new ProcessArgumentBuilder()
        .Append("sign")
        .Append("/fd SHA256")
        .Append("/f").AppendQuoted(certificatePath.FullPath)
        .Append($"/p").AppendQuotedSecret(certificatePassword);

    if (!string.IsNullOrWhiteSpace(display))
    {
        signArguments
            .Append("/d").AppendQuoted(display)
            .Append("/du").AppendQuoted(displayUrl);
    }

    foreach (var file in files)
    {
        signArguments.AppendQuoted(file.FullPath);
    }

    Information($"Executing: {signToolPath} {signArguments.RenderSafe()}");
    var exitCode = StartProcess(signToolPath, new ProcessSettings
    {
        Arguments = signArguments
    });

    if (exitCode != 0)
    {
        throw new Exception($"Signing files failed with the exit code {exitCode}. Look for 'SignTool Error' in the logs.");
    }

    Information($"Finished signing {files.Count()} files.");
}

private void TimeStampFiles(IEnumerable<FilePath> files)
{
    if (!FileExists(signToolPath))
    {
        throw new Exception($"The signing tool was expected to be at the path '{signToolPath}' but wasn't available.");
    }

    Information($"Timestamping {files.Count()} files...");

    var timestamped = false;
    foreach (var url in timestampUrls)
    {
        var timestampArguments = new ProcessArgumentBuilder()
            .Append($"timestamp")
            .Append("/tr").AppendQuoted(url);
        foreach (var file in files)
        {
            timestampArguments.AppendQuoted(file.FullPath);
        }

        try
        {
            Information($"Executing: {signToolPath} {timestampArguments.RenderSafe()}");
            var exitCode = StartProcess(signToolPath, new ProcessSettings
            {
                Arguments = timestampArguments
            });

            if (exitCode == 0)
            {
                timestamped = true;
                break;
            }
            else
            {
                throw new Exception($"Timestamping files failed with the exit code {exitCode}. Look for 'SignTool Error' in the logs.");
            }
        }
        catch (Exception ex)
        {
            Warning(ex.Message);
            Warning($"Failed to timestamp files using {url}. Maybe we can try another timestamp service...");
        }
    }

    if (!timestamped)
    {
        throw new Exception($"Failed to timestamp files even after we tried all of the timestamp services we use.");
    }

    Information($"Finished timestamping {files.Count()} files.");
}

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
        Configuration = configuration,
        NoBuild = true
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
    SignAndTimestampBinaries($"./source/Halibut/bin/{configuration}");
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
