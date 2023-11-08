using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Tentacle.Contracts;

namespace Halibut.Tests.TestServices
{
    public class AsyncFileTransferService : IAsyncFileTransferService
    {
        public async Task<UploadResult> UploadFileAsync(string remotePath, DataStream upload, CancellationToken cancellationToken)
        {
            await upload.Receiver().SaveToAsync(remotePath, CancellationToken.None);

            return new UploadResult(remotePath, Guid.NewGuid().ToString(), upload.Length);
        }

        public async Task<DataStream> DownloadFileAsync(string remotePath, CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            return new DataStream(
                new FileInfo(remotePath).Length,
                async (writer, ct) =>
                {
#if !NETFRAMEWORK
                    await
#endif
                    using (var stream = new FileStream(remotePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        await stream.CopyToAsync(writer);
                        await writer.FlushAsync(ct);
                    }
                });
        }
    }
}