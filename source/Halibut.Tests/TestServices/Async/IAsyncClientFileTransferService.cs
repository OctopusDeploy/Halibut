using System.Threading.Tasks;
using Octopus.Tentacle.Contracts;

namespace Halibut.Tests.TestServices.Async
{
    public interface IAsyncClientFileTransferService
    {
        Task<UploadResult> UploadFileAsync(string remotePath, DataStream upload);
        Task<DataStream> DownloadFileAsync(string remotePath);
    }
}
