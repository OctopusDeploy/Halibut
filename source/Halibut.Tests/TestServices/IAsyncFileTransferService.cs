using System;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Tentacle.Contracts;

namespace Halibut.Tests.TestServices
{
    public interface IAsyncFileTransferService
    {
        Task<UploadResult> UploadFileAsync(string remotePath, DataStream upload, CancellationToken cancellationToken);
        Task<DataStream> DownloadFileAsync(string remotePath, CancellationToken cancellationToken);
    }
}