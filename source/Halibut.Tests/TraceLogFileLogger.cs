using System;
using System.IO;

namespace Halibut.Tests
{
    public class TraceLogFileLogger : IDisposable
    {
        string tempFilePath = Path.GetTempFileName();
        string testHash;

        public void SetTestHash(string testHash)
        {
            this.testHash = testHash;
        }
        
        public void WriteLine(string logMessage)
        {
            File.AppendAllLines(tempFilePath, new[] { logMessage });
        }

        public bool CopyLogFileToArtifacts()
        {
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
