using System;
using System.Runtime.Serialization;
using Newtonsoft.Json.Serialization;

namespace Halibut.Transport.Protocol
{
    public class HalibutContractResolver : DefaultContractResolver
    {
        internal static IContractResolver Instance { get; } = new HalibutContractResolver();

        volatile bool HaveAddedCaptureOnSerializeCallback = false;
        
        public override JsonContract ResolveContract(Type type)
        {
            if (type == typeof(DataStream))
            {
                var contract = base.ResolveContract(type);
                // The contract is shared, so we need to make sure multiple threads don't try to edit it at the same time. 
                if (!HaveAddedCaptureOnSerializeCallback)
                {
                    lock (this)
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
            
            return base.ResolveContract(type);
        }

        static void CaptureOnSerialize(object o, StreamingContext context)
        {
            if (context.Context is StreamCapturingJsonSerializer.StreamCaptureContext contextCapture)
            {
                contextCapture.AddCapturedStream((DataStream)o);
            }
            else
            {
                throw new ArgumentException("context.Context should be of type StreamCapturingJsonSerializer.StreamCaptureContext");
            }
        }

        static void CaptureOnDeserialize(object o, StreamingContext context)
        {
            if (context.Context is StreamCapturingJsonSerializer.StreamCaptureContext contextCapture)
            {
                contextCapture.AddCapturedStream((DataStream)o);
            }
            else
            {
                throw new ArgumentException("context.Context should be of type StreamCapturingJsonSerializer.StreamCaptureContext");
            }
        }
    }
}