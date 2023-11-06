﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Threading;

namespace Halibut.Tests
{
    public class TraceLogFileLogger : IDisposable
    {
        readonly AsyncQueue<string> queue = new();
        readonly string tempFilePath = Path.GetTempFileName();
        string testHash;

        readonly TaskCompletionSource<bool> prepareForLogCollectionTaskCompletionSource;
        readonly Task prepareForLogCollection;
        readonly Task writeDataToDiskTask;

        public TraceLogFileLogger()
        {
            prepareForLogCollectionTaskCompletionSource = new TaskCompletionSource<bool>();
            prepareForLogCollection = prepareForLogCollectionTaskCompletionSource.Task;
            writeDataToDiskTask = WriteDataToFile();
        }

        public void SetTestHash(string testHash)
        {
            this.testHash = testHash;
        }

        public void WriteLine(string logMessage)
        {
            if (prepareForLogCollection.IsCompleted) return;
            queue.Enqueue(logMessage);
        }

        async Task WriteDataToFile()
        {
            while (!prepareForLogCollection.IsCompleted)
            {
                // Don't hammer the disk, let some log message queue up before writing them.
                if (!prepareForLogCollection.IsCompleted) await Task.WhenAny(Task.Delay(5), prepareForLogCollection);

                var list = new List<string>();
                list.Add(await queue.DequeueAsync());
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
            prepareForLogCollectionTaskCompletionSource.SetResult(false);
            writeDataToDiskTask.GetAwaiter().GetResult();
        }

        public bool CopyLogFileToArtifacts()
        {
            FinishWritingLogs();
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
                File.Copy(tempFilePath, Path.Combine(traceLogsDirectory.ToString(), fileName), true);
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