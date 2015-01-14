using System;
using System.Runtime.Serialization;
using Halibut.Protocol;
using Newtonsoft.Json.Serialization;

namespace Halibut.Services
{
    public class HalibutContractResolver : DefaultContractResolver
    {
        public override JsonContract ResolveContract(Type type)
        {
            var contract = base.ResolveContract(type);
            if (type == typeof(DataStream))
            {
                contract.OnSerializedCallbacks.Add(CaptureOnSerialize);
                contract.OnDeserializedCallbacks.Add(CaptureOnDeserialize);
            }

            return contract;
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