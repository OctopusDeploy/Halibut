using System;
using System.Linq;

namespace Octopus.TestPortForwarder
{
    /// <summary>
    /// Observes data flowing over a single proxied connection.
    /// </summary>
    public class BiDirectionalDataTransferObserver
    {
        public BiDirectionalDataTransferObserver(IDataTransferObserver dataTransferObserverClientToOrigin, IDataTransferObserver dataTransferObserverOriginToClient)
        {
            DataTransferObserverClientToOrigin = dataTransferObserverClientToOrigin;
            DataTransferObserverOriginToClient = dataTransferObserverOriginToClient;
        }

        public IDataTransferObserver DataTransferObserverClientToOrigin { get; }
        public IDataTransferObserver DataTransferObserverOriginToClient { get; }

        public static BiDirectionalDataTransferObserver Combiner(params BiDirectionalDataTransferObserver[] biDirectionalDataTransferObservers)
        {
            var dataTransferObserverClientToOrigin = DataTransferObserverCombiner.Combine(biDirectionalDataTransferObservers.Select(o => o.DataTransferObserverClientToOrigin).ToArray());
            var dataTransferObserverOriginToClient = DataTransferObserverCombiner.Combine(biDirectionalDataTransferObservers.Select(o => o.DataTransferObserverOriginToClient).ToArray());
            return new BiDirectionalDataTransferObserver(dataTransferObserverClientToOrigin, dataTransferObserverOriginToClient);
        }
    }
}