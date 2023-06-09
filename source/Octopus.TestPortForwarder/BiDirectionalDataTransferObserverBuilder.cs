using System;

namespace Octopus.TestPortForwarder
{
    public class BiDirectionalDataTransferObserverBuilder
    {
        IDataTransferObserver DataTransferObserverClientToOrigin = new DataTransferObserverBuilder().Build();
        IDataTransferObserver DataTransferObserverOriginToClient = new DataTransferObserverBuilder().Build();

        public BiDirectionalDataTransferObserverBuilder ObserveDataClientToOrigin(IDataTransferObserver DataTransferObserverClientToOrigin)
        {
            this.DataTransferObserverClientToOrigin = DataTransferObserverClientToOrigin;
            return this;
        }

        public BiDirectionalDataTransferObserverBuilder ObserveDataOriginToClient(IDataTransferObserver DataTransferObserverOriginToClient)
        {
            this.DataTransferObserverOriginToClient = DataTransferObserverOriginToClient;
            return this;
        }

        public BiDirectionalDataTransferObserver Build()
        {
            return new BiDirectionalDataTransferObserver(DataTransferObserverClientToOrigin, DataTransferObserverOriginToClient);
        }
    }
}