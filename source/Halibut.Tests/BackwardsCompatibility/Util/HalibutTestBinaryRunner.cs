using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Tests.Util;
using NUnit.Framework;
using Octopus.Shellfish;

namespace Halibut.Tests.BackwardsCompatibility.Util
{
    public class HalibutTestBinaryRunner
    {
        string BinaryDir(string version)
        {
            var onDiskVersion = version.Replace(".", "_");
            var assemblyDir = new DirectoryInfo(Path.GetDirectoryName(typeof(HalibutTestBinaryRunner).Assembly.Location)!);
            var upAt = assemblyDir.Parent.Parent.Parent.Parent;
            var executable = Path.Combine(upAt.FullName, $"Halibut.TestUtils.SampleProgram.v{onDiskVersion}", assemblyDir.Parent.Parent.Name, assemblyDir.Parent.Name, assemblyDir.Name, $"Halibut.TestUtils.SampleProgram.v{onDiskVersion}");
            executable = AddExeForWindows(executable);
            if (!File.Exists(executable))
            {
                throw new Exception("Could not executable at path:\n" +
                                    executable + "\n" +
                                    "Do you need to ask your IDE to build it?\n" +
                                    "If testing a previously untested version of Halibut a new project may be required.");
            }

            return executable;
        }

        string AddExeForWindows(string path)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return path + ".exe";
            return path;
        }

        public async Task<RunningOldHalibutBinary> Run(int octopusPort)
        {
            var commsUri = "https://localhost:" + octopusPort;
            var envs = new Dictionary<string, string>();
            envs.Add("tentaclecertpath", Certificates.TentaclePollingPfxPath);
            envs.Add("octopusthumbprint", Certificates.OctopusPublicThumbprint);
            envs.Add("octopusservercommsport", commsUri);

            CancellationTokenSource cts = new CancellationTokenSource();

            var tmp = new TmpDirectory();

            var task = await StartHalibutTestBinary(envs, tmp, cts.Token);

            return new RunningOldHalibutBinary(cts, task, tmp);
        }

        async Task<Task> StartHalibutTestBinary(Dictionary<string, string> envs, TmpDirectory tmp, CancellationToken cancellationToken)
        {
            var hasTentacleStarted = new ManualResetEventSlim();
            hasTentacleStarted.Reset();

            var runningTentacle = Task.Run(() =>
            {
                try
                {
                    Action<string> processLogs = s =>
                    {
                        TestContext.WriteLine(s);
                        if (s.Contains("RunningAndReady")) hasTentacleStarted.Set();
                    };

                    ShellExecutor.ExecuteCommand(BinaryDir("5.0.429"),
                        "",
                        tmp.FullPath,
                        processLogs,
                        processLogs,
                        processLogs,
                        customEnvironmentVariables: envs,
                        cancel: cancellationToken
                    );
                }
                catch (Exception e)
                {
                    TestContext.WriteLine(e);
                    throw;
                }
            }, cancellationToken);

            await Task.WhenAny(runningTentacle, Task.Run(() => { hasTentacleStarted.WaitHandle.WaitOne(TimeSpan.FromMinutes(1)); }));

            // Will throw.
            if (runningTentacle.IsCompleted) await runningTentacle;

            if (!hasTentacleStarted.IsSet)
            {
                throw new Exception("Halibut test binary did not appear to start correctly");
            }

            return runningTentacle;
        }

        public class RunningOldHalibutBinary : IDisposable
        {
            readonly CancellationTokenSource cts;
            readonly Task runningOldHalibutTask;
            readonly TmpDirectory tmpDirectory;

            public RunningOldHalibutBinary(CancellationTokenSource cts, Task runningOldHalibutTask, TmpDirectory tmpDirectory)
            {
                this.cts = cts;
                this.runningOldHalibutTask = runningOldHalibutTask;
                this.tmpDirectory = tmpDirectory;
            }

            public void Dispose()
            {
                cts.Cancel();
                runningOldHalibutTask.GetAwaiter().GetResult();
                tmpDirectory.Dispose();
            }
        }
    }
}