using System;
using Halibut.Diagnostics;

namespace Halibut
{
    public class HalibutTimeouts
    {
        public HalibutTimeouts()
        {
            TcpClientReceiveTimeout = HalibutLimits.TcpClientReceiveTimeout;
            TcpClientSendTimeout = HalibutLimits.TcpClientSendTimeout;
        }

        internal TimeSpan TcpClientReceiveTimeout { get; set; }
        
        internal TimeSpan TcpClientSendTimeout { get; set; }
    }
}