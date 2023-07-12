using System;
using System.IO;

namespace Halibut.TestUtils.SampleProgram.Base.Utils
{
    public static class DataStreamExtensionMethods
    {
        /// <summary>
        /// Data streams that are received do not have a writer set, and so can not be sent over the wire.
        /// This returns a new data stream with the writer set to received data
        /// </summary>
        /// <param name="receivedDataStream"></param>
        public static DataStream ConfigureWriterOnReceivedDataStream(this DataStream receivedDataStream)
        {
            return new DataStream(receivedDataStream.Length, stream => { receivedDataStream.Receiver().Read(dataStreamStream => dataStreamStream.CopyTo(stream)); });
        }
    }
}