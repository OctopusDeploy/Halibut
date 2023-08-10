using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Serilog;

namespace Octopus.TestPortForwarder
{
    public class DataTransferObserverBuilder
    {
        readonly List<Action<TcpPump, MemoryStream>> writingDataObserver = new ();
        
        public DataTransferObserverBuilder WithWritingDataObserver(Action<TcpPump, MemoryStream> writingDataObserver)
        {
            this.writingDataObserver.Add(writingDataObserver);
            return this;
        }

        public DataTransferObserverBuilder WithWritePausing(ILogger logger, int writeNumberToPauseOn)
        {
            var hasPausedAConnection = false;
            var numberOfWritesSeen = 0;

            return WithWritingDataObserver((tcpPump, _) =>
            {
                Interlocked.Increment(ref numberOfWritesSeen);
                if (!hasPausedAConnection && numberOfWritesSeen == writeNumberToPauseOn)
                {
                    hasPausedAConnection = true;
                    logger.Information("Pausing pump");
                    tcpPump.Pause();
                }
            });
        }

        public IDataTransferObserver Build()
        {
            return new ActionDataTransferObserver(writingDataObserver);
        }

        class ActionDataTransferObserver : IDataTransferObserver
        {
            readonly List<Action<TcpPump, MemoryStream>> writingDataObserver;

            public ActionDataTransferObserver(List<Action<TcpPump, MemoryStream>> writingDataObserver)
            {
                this.writingDataObserver = writingDataObserver;
            }

            public void WritingData(TcpPump tcpPump, MemoryStream buffer)
            {
                foreach (var action in writingDataObserver)
                {
                    action(tcpPump, buffer);
                }
            }
        }
    }
}