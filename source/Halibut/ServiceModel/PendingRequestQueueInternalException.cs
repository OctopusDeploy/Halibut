using System;

namespace Halibut.ServiceModel
{
    public class PendingRequestQueueInternalException : Exception 
    {
        public PendingRequestQueueInternalException(string s) : base(s)
        {
        }
    }
}