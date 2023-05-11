using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Tests.TestServices;
using Halibut.Tests.Util;
using NUnit.Framework;
using Octopus.Shellfish;

namespace Halibut.Tests.BackwardsCompatibility;

public class WithOldHalibutPolling
{
    [Test]
    public async Task DoesItWork()
    {
        var assemblyDir = new DirectoryInfo(Path.GetDirectoryName(typeof(WithOldHalibutPolling).Assembly.Location)!);
        var upAt = assemblyDir.Parent.Parent.Parent.Parent;

        var OldMateDir = Path.Combine(upAt.FullName, "OldMate", assemblyDir.Parent.Parent.Name, assemblyDir.Parent.Name, assemblyDir.Name, "OldMate");

        using( var tmp = new TmpDirectory())
        using (var octopus = new HalibutRuntime(Certificates.Octopus))
        {
            var octopusPort = octopus.Listen();
            octopus.Trust(Certificates.TentaclePollingPublicThumbprint);
            var commsUri = "https://localhost:" + octopusPort;

            var stdOutBuffer = new StringBuilder();
            var stdErrBuffer = new StringBuilder();
            CancellationTokenSource cts = new CancellationTokenSource();
            try
            {
                // var runningTentacle = Cli.Wrap(OldMateDir)
                //     .WithWorkingDirectory("/tmp/")
                //     .WithEnvironmentVariables(c => c.Set("tentaclecertpath", Certificates.TentaclePollingPfxPath)
                //         .Set("octopusthumbprint", Certificates.OctopusPublicThumbprint)
                //         .Set("octopusservercommsport", commsUri))
                //     .WithStandardOutputPipe(PipeTarget.ToStringBuilder(stdOutBuffer))
                //     .WithStandardErrorPipe(PipeTarget.ToStringBuilder(stdErrBuffer))
                //     .ExecuteAsync(cts.Token);


                var envs = new Dictionary<string, string>();
                envs.Add("tentaclecertpath", Certificates.TentaclePollingPfxPath);
                envs.Add("octopusthumbprint", Certificates.OctopusPublicThumbprint);
                envs.Add("octopusservercommsport", commsUri);

                var task = Task.Run(() =>
                {
                    ShellExecutor.ExecuteCommand(OldMateDir,
                        "",
                        tmp.FullPath,
                        TestContext.Out.WriteLine,
                        TestContext.Out.WriteLine,
                        TestContext.Out.WriteLine,
                        customEnvironmentVariables: envs,
                        cancel: cts.Token
                    );
                });

                await Task.WhenAny(task, Task.Delay(100));
                if (task.IsCompleted) await task;
                

                var se = new ServiceEndPoint("poll://SQ-TENTAPOLL", Certificates.TentaclePollingPublicThumbprint);

                se.PollingRequestQueueTimeout = TimeSpan.FromSeconds(5);
                se.PollingRequestMaximumMessageProcessingTimeout = TimeSpan.FromSeconds(10);
                var echo = octopus.CreateClient<IEchoService>(se);

                var res = echo.SayHello("sd");

                res.Should().Be("sd");
            }
            finally
            {
                cts.Cancel();
                TestContext.WriteLine(stdOutBuffer);
                TestContext.WriteLine(stdErrBuffer);
            }
        }
    }
}