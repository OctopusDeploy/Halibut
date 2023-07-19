using Halibut.TestUtils.SampleProgram.Base.Utils;
using Octopus.Tentacle.Contracts;

namespace Halibut.TestUtils.SampleProgram.Base
{
    public class DelegateFileTransferService : IFileTransferService
    {
        readonly IFileTransferService service;

        public DelegateFileTransferService(IFileTransferService service)
        {
            this.service = service;
        }

        public UploadResult UploadFile(string remotePath, DataStream upload)
        {
            return service.UploadFile(remotePath, upload.ConfigureWriterOnReceivedDataStream());
        }

        public DataStream DownloadFile(string remotePath)
        {
            return service.DownloadFile(remotePath).ConfigureWriterOnReceivedDataStream();
        }
    }
}
