using System;
using System.Collections.Generic;
using System.IO;
using Halibut.Tests.Util.TcpUtils;

namespace Halibut.TestUtils.PortForwarder.TcpUtils
{
    public class DataTransferObserverBuilder
    {
        private List<Action<TcpPump, MemoryStream>> WritingDataObserver = new ();
        
        public DataTransferObserverBuilder WithWritingDataObserver(Action<TcpPump, MemoryStream> WritingDataObserver)
        {
            this.WritingDataObserver.Add(WritingDataObserver);
            return this;
        }
        
        public IDataTransferObserver Build()
        {
            return new ActionDataTransferObserver(WritingDataObserver);
        }

        private class ActionDataTransferObserver : IDataTransferObserver
        {
            private List<Action<TcpPump, MemoryStream>> WritingDataObserver;

            public ActionDataTransferObserver(List<Action<TcpPump, MemoryStream>> writingDataObserver)
            {
                WritingDataObserver = writingDataObserver;
            }

            public void WritingData(TcpPump tcpPump, MemoryStream buffer)
            {
                foreach (var action in WritingDataObserver)
                {
                    action(tcpPump, buffer);
                }
            }
        }
    }
}