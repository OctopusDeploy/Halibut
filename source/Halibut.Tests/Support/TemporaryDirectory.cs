using System;
using System.IO;
using System.Reflection;

namespace Halibut.Tests.Support
{
    public class TemporaryDirectory : IDisposable
    {
        private bool deleted;

        public TemporaryDirectory(string? directoryPath = null)
        {
            DirectoryPath = directoryPath ?? CreateTemporaryDirectory();
        }

        public string DirectoryPath { get; }

        public string CreateRandomFile()
        {
            string randomFile = RandomFileName();
            File.Create(randomFile);
            return randomFile;
        }

        public string RandomFileName()
        {
            return Path.Combine(DirectoryPath, Guid.NewGuid().ToString());
        }

        string GetTempBasePath()
        {
            var path = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.DoNotVerify);
            Directory.CreateDirectory(path);

            path = Path.Combine(path, Assembly.GetEntryAssembly() != null ? Assembly.GetEntryAssembly()!.GetName().Name! : "Octopus");
            return Path.Combine(path, "Temp");
        }

        string CreateTemporaryDirectory()
        {
            var path = Path.Combine(GetTempBasePath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(path);
            return path;
        }

        public (bool deleted, Exception? deleteException) TryDelete()
        {
            if (!deleted)
            {
                if (Directory.Exists(DirectoryPath))
                {
                    try
                    {
                        Directory.Delete(DirectoryPath, true);
                    }
                    catch (Exception e)
                    {
                        return (false, e);
                    }
                }

                deleted = true;
            }

            return (true, null);
        }

        public void Dispose()
        {
            TryDelete();
        }
    }
}