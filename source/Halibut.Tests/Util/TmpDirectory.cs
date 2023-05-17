using System;
using System.IO;
using System.Reflection;

namespace Halibut.Tests.Util
{
    public class TmpDirectory : IDisposable
    {
        public readonly string FullPath;

        public TmpDirectory()
        {
            FullPath = CreateTemporaryDirectory();
        }

        string GetTempBasePath()
        {
            var path = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.DoNotVerify);
            Directory.CreateDirectory(path);

            path = Path.Combine(path, Assembly.GetEntryAssembly() != null ? Assembly.GetEntryAssembly().GetName().Name : "Octopus");
            return Path.Combine(path, "Temp");
        }

        string CreateTemporaryDirectory()
        {
            var path = Path.Combine(GetTempBasePath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(path);
            return path;
        }

        public void Dispose()
        {
            RecursiveDelete(new DirectoryInfo(FullPath));
        }

        public static void RecursiveDelete(DirectoryInfo baseDir)
        {
            if (!baseDir.Exists)
                return;

            foreach (var dir in baseDir.EnumerateDirectories())
            {
                RecursiveDelete(dir);
            }

            baseDir.Delete(true);
        }
    }
}