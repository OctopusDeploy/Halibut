using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
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
        
        public async Task SaveToAsync(string filePath, CancellationToken cancellationToken)
        {
            if (moved) throw new InvalidOperationException("This stream has already been received once, and it cannot be read again.");

            await AttemptToDeleteAsync(filePath);
            using (FileStream sourceStream = File.Open(path, FileMode.Open))
            {
                using (FileStream destinationStream = File.Create(filePath))
                {
                    await sourceStream.CopyToAsync(destinationStream);
                }
            }
            await AttemptToDeleteAsync(path);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                SetFilePermissionsToInheritFromParent(filePath);
            }

            moved = true;
            GC.SuppressFinalize(this);
        }

        void SetFilePermissionsToInheritFromParent(string filePath)
        {
            try
            {
                var fileInfo = new FileInfo(filePath);
#pragma warning disable CA1416 // API not supported on all platforms
                var fileSecurity = fileInfo.GetAccessControl();

                //When isProtected (first param) is false, SetAccessRuleProtection changes the permissions of the file to allow inherited permissions.
                //preserveInheritance (second param) is ignored when isProtected is false.
                fileSecurity.SetAccessRuleProtection(false, false);
                fileInfo.SetAccessControl(fileSecurity);
#pragma warning restore CA1416 // API not supported on all platforms
            }
            catch (UnauthorizedAccessException ex)
            {
                log?.Write(EventType.Security, $"Ignoring an unauthorized access issue: {ex.Message}. {nameof(TemporaryFileStream)} assumes that filesystem permissions allow full control to the executing user for {filePath}. Update those permissions to remove this log.");
            }
        }
        
        public async Task ReadAsync(Func<Stream, CancellationToken, Task> readerAsync, CancellationToken cancellationToken)
        {
            if (moved) throw new InvalidOperationException("This stream has already been received once, and it cannot be read again.");
#if !NETFRAMEWORK
            await
#endif
            using (var file = new FileStream(path, FileMode.Open, FileAccess.Read))
            {
                await readerAsync(file, cancellationToken);
            }
            await AttemptToDeleteAsync(path);
            moved = true;
            GC.SuppressFinalize(this);
        }
        
        static async Task AttemptToDeleteAsync(string fileToDelete)
        {
            for (var i = 1; i <= 3; i++)
            {
                try
                {
                    // dotnet does not have an sync implementation of File.Delete
                    if (File.Exists(fileToDelete))
                    {
                        File.Delete(fileToDelete);
                    }
                }
                catch (Exception)
                {
                    if (i == 3)
                        throw;
                    await Task.Delay(TimeSpan.FromSeconds(1));
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
