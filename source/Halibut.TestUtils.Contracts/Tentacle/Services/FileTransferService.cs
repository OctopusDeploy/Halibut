using System;
using System.IO;
using Octopus.Tentacle.Contracts;

namespace Halibut.TestUtils.Contracts.Tentacle.Services
{
    public class FileTransferService : IFileTransferService
    {
        public UploadResult UploadFile(string remotePath, DataStream upload)
        {
            upload.Receiver().SaveTo(remotePath);

            return new UploadResult(remotePath, Guid.NewGuid().ToString(), upload.Length);
        }

        public DataStream DownloadFile(string remotePath)
        {
            return new DataStream(new FileInfo(remotePath).Length, writer =>
            {
                using (var stream = new FileStream(remotePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    stream.CopyTo(writer);
                    writer.Flush();
                }
            });
        }
    }
}
