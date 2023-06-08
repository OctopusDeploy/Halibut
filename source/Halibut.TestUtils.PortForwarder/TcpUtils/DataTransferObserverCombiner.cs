using System;
using Halibut.Tests.Util.TcpUtils;

namespace Halibut.TestUtils.PortForwarder.TcpUtils
{
    public class DataTransferObserverCombiner
    {
        public static IDataTransferObserver Combine(params IDataTransferObserver[] dataTransferObservers)
        {
            var builder = new DataTransferObserverBuilder();
            foreach (var dataTransferObserver in dataTransferObservers)
            {
                builder.WithWritingDataObserver(dataTransferObserver.WritingData);
            }

            return builder.Build();
        }
    }
}