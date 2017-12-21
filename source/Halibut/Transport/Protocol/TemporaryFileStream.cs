using System;
using System.IO;
using System.Threading;

namespace Halibut.Transport.Protocol
{
    public class TemporaryFileStream : IDataStreamReceiver
    {
        readonly string path;
        bool moved;

        public TemporaryFileStream(string path)
        {
            this.path = path;
        }

        public void SaveTo(string filePath)
        {
            if (moved) throw new InvalidOperationException("This stream has already been received once, and it cannot be read again.");

            AttemptToDelete(filePath);
            File.Move(path, filePath);
            SetFilePermissionsToInheritFromParent(filePath);
            moved = true;
            GC.SuppressFinalize(this);
        }

        void SetFilePermissionsToInheritFromParent(string filePath)
        {
            var fileInfo = new FileInfo(filePath);
            var fileSecurity = fileInfo.GetAccessControl();

            //When isProtected (first param) is false, SetAccessRuleProtection changes the permissions of the file to allow inherited permissions. 
            //preserveInheritance (second param) is ignored when isProtected is false.
            fileSecurity.SetAccessRuleProtection(false, false);
            fileInfo.SetAccessControl(fileSecurity);
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