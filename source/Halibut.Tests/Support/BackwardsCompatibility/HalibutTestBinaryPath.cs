using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Halibut.Tests.Support.BackwardsCompatibility
{
    public class HalibutTestBinaryPath
    {
        public string BinPath(string version)
        {
            var onDiskVersion = version.Replace(".", "_");
            var assemblyDir = new DirectoryInfo(Path.GetDirectoryName(typeof(HalibutTestBinaryRunner).Assembly.Location)!);
            var upAt = assemblyDir.Parent!.Parent!.Parent!.Parent!;
            var projectName = $"Halibut.TestUtils.CompatBinary.v{onDiskVersion}";
            var executable = Path.Combine(upAt.FullName, projectName, assemblyDir.Parent.Parent.Name, assemblyDir.Parent.Name, assemblyDir.Name, projectName);
            executable = AddExeForWindows(executable);

            if (!File.Exists(executable))
            {
                throw new Exception("Could not executable at path:\n" +
                                    executable + "\n" +
                                    $"Did you forget to update the csproj to depend on {projectName}\n" +
                                    "If testing a previously untested version of Halibut a new project may be required.");
            }

            return executable;
        }
        
        string AddExeForWindows(string path)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return path + ".exe";
            return path;
        }
    }
}