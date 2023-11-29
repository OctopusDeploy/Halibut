using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Threading;

namespace Halibut.Tests
{
    public class TraceLogFileLogger : IDisposable
    {
        readonly AsyncQueue<string> queue = new();
        public readonly string logFilePath;
        readonly string testHash;

        readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        readonly Task writeDataToDiskTask;

        public TraceLogFileLogger(string testHash)
        {
            this.testHash = testHash;
            this.logFilePath = LogFilePath(testHash);
            File.Delete(logFilePath);

            writeDataToDiskTask = WriteDataToFile();
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

                using(var fileWriter = new FileStream(logFilePath, FileMode.Append, FileAccess.Write, FileShare.Delete | FileShare.ReadWrite))
                using (var fileAppender = new StreamWriter(fileWriter, Encoding.UTF8, 8192))
                {
                    foreach (var logLine in list) await fileAppender.WriteLineAsync(logLine);

                    await fileAppender.FlushAsync();
                }
            }
        }

        

        static string LogFilePath(string testHash)
        {
            var traceLogsDirectory = LogFileDirectory();
            var fileName = $"{testHash}.tracelog";
            var logFilePath = Path.Combine(traceLogsDirectory.ToString(), fileName);
            return logFilePath;
        }

        public static DirectoryInfo LogFileDirectory()
        {
            // The current directory is expected to have the following structure
            // (w/ variance depending on Debug/Release and dotnet framework used (net6.0, net48 etc):
            //
            // <REPO ROOT>\source\Halibut.Tests\bin\Debug\net6.0
            //
            // Therefore we go up 5 levels to get to the <REPO ROOT> directory,
            // from which point we can navigate to the artifacts directory.
            var currentDirectory = Directory.GetCurrentDirectory();
            var rootDirectory = new DirectoryInfo(currentDirectory).Parent!.Parent!.Parent!.Parent!.Parent!;

            var traceLogsDirectory = rootDirectory.CreateSubdirectory("artifacts").CreateSubdirectory("trace-logs");
            return traceLogsDirectory;
        }

        public void Dispose()
        {
            cancellationTokenSource.Cancel();
#pragma warning disable VSTHRD002 // Avoid problematic synchronous waits
            writeDataToDiskTask.GetAwaiter().GetResult();
#pragma warning restore VSTHRD002 // Avoid problematic synchronous waits
            cancellationTokenSource.Dispose();
        }
    }
}