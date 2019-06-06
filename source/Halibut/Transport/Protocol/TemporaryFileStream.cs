using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Halibut.Diagnostics;

namespace Halibut.Transport.Protocol
{
    public class TemporaryFileStream : IDataStreamReceiver
    {
        readonly string path;
        readonly ILog log;
        bool moved;

        public TemporaryFileStream(string path, ILog log)
        {
            this.path = path;
            this.log = log;
        }

        public void SaveTo(string filePath)
        {
            if (moved) throw new InvalidOperationException("This stream has already been received once, and it cannot be read again.");

            AttemptToDelete(filePath);
            File.Move(path, filePath);
#if HAS_RUNTIME_INFORMATION
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                SetFilePermissionsToInheritFromParent(filePath);
            }
#else
            SetFilePermissionsToInheritFromParent(filePath);
#endif

            moved = true;
            GC.SuppressFinalize(this);
        }

        void SetFilePermissionsToInheritFromParent(string filePath)
        {
            try
            {
                var fileInfo = new FileInfo(filePath);
#pragma warning disable PC001 // API not supported on all platforms
                var fileSecurity = fileInfo.GetAccessControl();

                //When isProtected (first param) is false, SetAccessRuleProtection changes the permissions of the file to allow inherited permissions. 
                //preserveInheritance (second param) is ignored when isProtected is false.
                fileSecurity.SetAccessRuleProtection(false, false);
                fileInfo.SetAccessControl(fileSecurity);
#pragma warning restore PC001 // API not supported on all platforms
            }
            catch (UnauthorizedAccessException ex)
            {
                log?.Write(EventType.Security, $"Ignoring an unauthorized access issue: {ex.Message}. {nameof(TemporaryFileStream)} assumes that filesystem permissions allow full control to the executing user for {filePath}. Update those permissions to remove this log.");
            }
        }

        public void Read(Action<Stream> reader)
        {
            if (moved) throw new InvalidOperationException("This stream has already been received once, and it cannot be read again.");

            using (var file = new FileStream(path, FileMode.Open, FileAccess.Read))
            {
                reader(file);
            }
            AttemptToDelete(path);
            moved = true;
            GC.SuppressFinalize(this);
        }

        static void AttemptToDelete(string fileToDelete)
        {
            for (var i = 1; i <= 3; i++)
            {
                try
                {
                    if (File.Exists(fileToDelete))
                    {
                        File.Delete(fileToDelete);
                    }
                }
                catch (Exception)
                {
                    if (i == 3)
                        throw;
                    Thread.Sleep(1000);
                }
            }
        }

        // Ensure that if a receiver doesn't process a file, we still clean ourselves up
        ~TemporaryFileStream()
        {
            if (moved) 
                return;

            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception)
            {
                // ignored - can't throw in the GC
            }
        }
    }
}