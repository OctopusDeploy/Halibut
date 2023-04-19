using System;

namespace Halibut
{
    public class TransferStatistics
    {
        public TransferStatistics(TimeSpan span, long bytesTransferred)
        {
            this.span = span;
            BytesTransferred = bytesTransferred;
        }
        public TimeSpan span { get; }
        public long BytesTransferred { get; }
    }
}