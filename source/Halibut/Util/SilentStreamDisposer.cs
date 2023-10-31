using System;
using System.IO;
using System.Threading.Tasks;
using Halibut.Transport.Streams;

namespace Halibut.Util
{
    public class SilentStreamDisposer : IAsyncDisposable
    {
        readonly Stream streamToDispose;
        readonly Action<Exception> onFailure;

        public SilentStreamDisposer(Stream streamToDispose, Action<Exception> onFailure)
        {
            this.streamToDispose = streamToDispose;
            this.onFailure = onFailure;
        }
        
        public async ValueTask DisposeAsync()
        {
            try
            {
                await streamToDispose.DisposeAsync();
            }
            catch (Exception ex)
            {
                onFailure.Invoke(ex);
            }
        }
    }
}