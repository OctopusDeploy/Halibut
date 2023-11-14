using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.TeamCity.ServiceMessages.Write;
using JetBrains.TeamCity.ServiceMessages.Write.Special;
using Microsoft.VisualStudio.Threading;

namespace Halibut.Tests
{
    public class TraceLogFileLogger : IDisposable
    {
        readonly AsyncQueue<string> queue = new();
        readonly string tempFilePath = Path.GetTempFileName();
        string testHash;
        string testName;

        readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        readonly Task writeDataToDiskTask;

        public TraceLogFileLogger()
        {
            writeDataToDiskTask = WriteDataToFile();
        }

        public void SetTestHash(string testHash)
        {
            this.testHash = testHash;
        }

        public void SetTestName(string name)
        {
            this.testName = name;
        }

        public void WriteLine(string logMessage)
        {
            if (cancellationTokenSource.IsCancellationRequested) return;
            queue.Enqueue(logMessage);
        }

        async Task WriteDataToFile()
        {
            while (!cancellationTokenSource.IsCancellationRequested)
            {
                var list = new List<string>();

                try
                {
                    // Don't hammer the disk, let some log message queue up before writing them.
                    await Task.Delay(TimeSpan.FromMilliseconds(5), cancellationTokenSource.Token);

                    // await here for something to enter the queue.
                    list.Add(await queue.DequeueAsync(cancellationTokenSource.Token));
                }
                catch (OperationCanceledException)
                {
                }

                // If we got something from the queue, get as much as we can from queue without blocking.
                // So what we can write it down as one chunk.
                while (queue.TryDequeue(out var log)) list.Add(log);

                using (var fileAppender = new StreamWriter(tempFilePath, true, Encoding.UTF8, 8192))
                {
                    foreach (var logLine in list) await fileAppender.WriteLineAsync(logLine);

                    await fileAppender.FlushAsync();
                }
            }
        }

        void FinishWritingLogs()
        {
            cancellationTokenSource.Cancel();
            writeDataToDiskTask.GetAwaiter().GetResult();
            cancellationTokenSource.Dispose();
        }

        public bool CopyLogFileToArtifacts()
        {
            FinishWritingLogs();
            return CopyFileToArtifactsDirectory();
        }

        bool CopyFileToArtifactsDirectory()
        {

            // The current directory is expected to have the following structure
            // (w/ variance depending on Debug/Release and dotnet framework used (net6.0, net48 etc):
            //
            // <REPO ROOT>\source\Halibut.Tests\bin\Debug\net6.0
            //
            // Therefore we go up 5 levels to get to the <REPO ROOT> directory,
            // from which point we can navigate to the artifacts directory.
            var currentDirectory = Directory.GetCurrentDirectory();
            var rootDirectory = new DirectoryInfo(currentDirectory).Parent.Parent.Parent.Parent.Parent;

            var traceLogsDirectory = rootDirectory.CreateSubdirectory("artifacts").CreateSubdirectory("trace-logs");
            var fileName = $"{testHash}.tracelog";


            try
            {
                var traceLogFilePath = Path.Combine(traceLogsDirectory.ToString(), fileName);
                File.Copy(tempFilePath, traceLogFilePath, true);

                using var teamCityWriter = new TeamCityServiceMessages().CreateWriter(Console.WriteLine);
                teamCityWriter.PublishArtifact($"{traceLogFilePath} => adrian-test-trace-logs");
                var artifactUri = $"adrian-test-trace-logs/{fileName}";
                using var testWriter = teamCityWriter.OpenTest(testName);
                testWriter.WriteValue("some random value", "Adrian test value");
                testWriter.WriteFile(artifactUri, "Trace logs");
                teamCityWriter.WriteRawMessage(new ServiceMessage("testMetadata")
                {
                    { "testName", testName },
                    { "type", "artifact" },
                    { "value", artifactUri }
                });

                return true;
            }
            catch
            {
                return false;
            }
        }

        public void Dispose()
        {
            try
            {
                File.Delete(tempFilePath);
            }
            catch
            {
                // Best effort clean-up, but we don't want to
                // fail the test because we couldn't delete this file
            }
        }
    }
}
