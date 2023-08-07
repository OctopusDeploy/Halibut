using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Logging;
using Halibut.Tests.Builders;
using Halibut.Tests.Support;
using Halibut.Tests.Support.TestAttributes;
using Halibut.Tests.Support.TestCases;
using NUnit.Framework;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.Capabilities;
using Octopus.Tentacle.Contracts.ScriptServiceV2;

namespace Halibut.Tests.Tentacle
{
    public class WhenCallingServicesSimilarToTheOnesInTentacle : BaseTest
    {
        // TODO - ASYNC ME UP!
        const bool TentacleTestAsyncAndSyncClients = false;
        [Test]
        [LatestAndPreviousClientAndServiceVersionsTestCases(testNetworkConditions: false, testAsyncAndSyncClients: TentacleTestAsyncAndSyncClients)]
        public async Task FilesCanBeDownloaded(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .WithTentacleServices()
                       .Build(CancellationToken))
            {
                var fileTransferService = clientAndService.CreateClient<IFileTransferService>();

                DownloadFile(fileTransferService);
                DownloadFile(fileTransferService);
                DownloadFile(fileTransferService);
                DownloadFile(fileTransferService);
            }

            static void DownloadFile(IFileTransferService fileTransferService)
            {
                using (var fileToDownload = new RandomTemporaryFileBuilder().WithSizeInMb(new Random().Next(4, 12)).Build())
                using (var temporaryFolder = new TemporaryDirectory())
                {
                    var response = fileTransferService.DownloadFile(fileToDownload.File.FullName);
                    var downloadedFilePath = Path.Combine(temporaryFolder.DirectoryPath, fileToDownload.File.Name);
                    response.Receiver().SaveTo(downloadedFilePath);

                    var fileToDownloadMd5 = CalculateMd5(fileToDownload.File.FullName);
                    var downloadedFileMd5 = CalculateMd5(downloadedFilePath);

                    downloadedFileMd5.Should().Be(fileToDownloadMd5);
                }
            }
        }

        [Test]
        [LatestAndPreviousClientAndServiceVersionsTestCases(testNetworkConditions: false, testAsyncAndSyncClients: TentacleTestAsyncAndSyncClients)]
        public async Task FilesCanBeUploaded(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .WithTentacleServices()
                       .Build(CancellationToken))
            {
                var fileTransferService = clientAndService.CreateClient<IFileTransferService>();

                UploadFile(fileTransferService);
                UploadFile(fileTransferService);
                UploadFile(fileTransferService);
                UploadFile(fileTransferService);
            }

            static void UploadFile(IFileTransferService fileTransferService)
            {
                using (var fileToUpload = new RandomTemporaryFileBuilder().WithSizeInMb(new Random().Next(4, 12)).Build())
                using (var temporaryFolder = new TemporaryDirectory())
                {
                    var uploadedFilePath = Path.Combine(temporaryFolder.DirectoryPath, Guid.NewGuid().ToString());

                    var dataStream = new DataStream(fileToUpload.File.Length, writer =>
                    {
                        using (var stream = new FileStream(fileToUpload.File.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        {
                            stream.CopyTo(writer);
                            writer.Flush();
                        }
                    });

                    fileTransferService.UploadFile(uploadedFilePath, dataStream);

                    var fileToUploadMd5 = CalculateMd5(fileToUpload.File.FullName);
                    var uploadedFileMd5 = CalculateMd5(uploadedFilePath);

                    uploadedFileMd5.Should().Be(fileToUploadMd5);
                }
            }
        }

        [Test]
        [LatestAndPreviousClientAndServiceVersionsTestCases(testNetworkConditions: false, testAsyncAndSyncClients: TentacleTestAsyncAndSyncClients)]
        public async Task ScriptCanBeExecutedWithScriptServiceV1(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .WithTentacleServices()
                       .Build(CancellationToken))
            {
                var scriptService = clientAndService.CreateClient<IScriptService>();
                var capabilitiesService = clientAndService.CreateClient<ICapabilitiesServiceV2>();

                var scriptBody = GetRandomMultiLineString();
                var startScriptCommand = new StartScriptCommand(scriptBody, ScriptIsolationLevel.NoIsolation, TimeSpan.MaxValue, null, null, taskId: Guid.NewGuid().ToString());

                capabilitiesService.GetCapabilities();

                var scriptTicket = scriptService.StartScript(startScriptCommand);

                var complete = false;
                var logs = new List<ProcessOutput>();
                long nextLogSequence = 0;

                while (!complete)
                {
                    var response = scriptService.GetStatus(new ScriptStatusRequest(scriptTicket, nextLogSequence));

                    logs.AddRange(response.Logs);
                    nextLogSequence = response.NextLogSequence;

                    complete = response.State == ProcessState.Complete;
                }

                var completeScriptResponse = scriptService.CompleteScript(new CompleteScriptCommand(scriptTicket, nextLogSequence));
                logs.AddRange(completeScriptResponse.Logs);

                var scriptOutput = string.Join(Environment.NewLine, logs.Select(x => x.Text).ToList());

                NormalizeLineEndings(scriptOutput).Should().Be(NormalizeLineEndings(scriptBody));
            }
        }

        [Test]
        [LatestAndPreviousClientAndServiceVersionsTestCases(testNetworkConditions: false, testAsyncAndSyncClients: TentacleTestAsyncAndSyncClients)]
        public async Task ScriptCanBeExecutedWithScriptServiceV2(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            using (var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                       .WithTentacleServices()
                       .Build(CancellationToken))
            {
                var scriptService = clientAndService.CreateClient<IScriptServiceV2>();
                var capabilitiesService = clientAndService.CreateClient<ICapabilitiesServiceV2>();

                var scriptBody = GetRandomMultiLineString();
                var scriptTicket = new ScriptTicket(Guid.NewGuid().ToString());
                var startScriptCommand = new StartScriptCommandV2(scriptBody, ScriptIsolationLevel.NoIsolation, TimeSpan.MaxValue, null, null, scriptTicket.TaskId, scriptTicket, TimeSpan.Zero);

                var complete = false;
                var logs = new List<ProcessOutput>();
                long nextLogSequence = 0;

                capabilitiesService.GetCapabilities();

                var startScriptResponse = scriptService.StartScript(startScriptCommand);
                logs.AddRange(startScriptResponse.Logs);
                nextLogSequence = startScriptResponse.NextLogSequence;

                while (!complete)
                {
                    var response = scriptService.GetStatus(new ScriptStatusRequestV2(scriptTicket, nextLogSequence));
                    logs.AddRange(response.Logs);
                    nextLogSequence = response.NextLogSequence;

                    complete = response.State == ProcessState.Complete;
                }

                scriptService.CompleteScript(new CompleteScriptCommandV2(scriptTicket));
     
                var scriptOutput = string.Join(Environment.NewLine, logs.Select(x => x.Text).ToList());

                NormalizeLineEndings(scriptOutput).Should().Be(NormalizeLineEndings(scriptBody));
            }
        }

        static string NormalizeLineEndings(string s)
        {
            return s.Replace("\r\n", "\n").Replace("\r", "\n");
        }

        string GetRandomMultiLineString()
        {
            string GetGuidString()
            {
                return Guid.NewGuid().ToString().Replace("-", " ");
            }

            var lines = new Random().Next(66, 166);

            var builder = new StringBuilder();

            for (var i = 0; i < lines; i++)
            {
                builder.AppendLine($"{GetGuidString()}{GetGuidString()}");
            }

            return builder.ToString();
        }

        static string CalculateMd5(string filename)
        {
            using var md5 = MD5.Create();
            using var stream = File.OpenRead(filename);
            var hash = md5.ComputeHash(stream);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }
    }
}
