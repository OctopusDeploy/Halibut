using System;
using System.Runtime.Serialization;
using Newtonsoft.Json.Serialization;

namespace Halibut.Transport.Protocol
{
    public class HalibutContractResolver : IContractResolver
    {
        static IContractResolver baseContractResolver = new DefaultContractResolver();
        static volatile bool HaveAddedCaptureOnSerializeCallback = false;
        
        public JsonContract ResolveContract(Type type)
        {
            if (type == typeof(DataStream))
            {
                var contract = baseContractResolver.ResolveContract(type);
                // The contract is globally shared, so we need to make sure multiple threads don't try to edit it at the same time. 
                if (!HaveAddedCaptureOnSerializeCallback)
                {
                    lock (baseContractResolver)
                    {
                        if (!HaveAddedCaptureOnSerializeCallback)
                        {
                            contract.OnSerializedCallbacks.Add(CaptureOnSerialize);
                            contract.OnDeserializedCallbacks.Add(CaptureOnDeserialize);
                            HaveAddedCaptureOnSerializeCallback = true;
                        }
                    }
                    
                }
                
                return contract;
            }
            
            return baseContractResolver.ResolveContract(type);
        }

        static void CaptureOnSerialize(object o, StreamingContext context)
        {
            var capture = StreamCapture.Current;
            if (capture != null)
            {
                capture.SerializedStreams.Add((DataStream)o);
            }
        }

        static void CaptureOnDeserialize(object o, StreamingContext context)
        {
            var capture = StreamCapture.Current;
            if (capture != null)
            {
                capture.DeserializedStreams.Add((DataStream)o);
            }
        }
    }
}