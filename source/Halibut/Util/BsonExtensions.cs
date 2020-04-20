using System;
using System.IO;
using Halibut.Transport.Protocol;
using Newtonsoft.Json.Bson;

namespace Halibut.Util
{
    public static class BsonExtensions
    {
        public static T FromBson<T>(this byte[] data)
        {
            using(MemoryStream ms = new MemoryStream(data))
            using (BsonDataReader reader = new BsonDataReader(ms))
            {
                return MessageExchangeStream.Serializer().Deserialize<T>(reader);
            }
        }
        
        public static byte[] ToBson<T>(this T value)
        {
            using (MemoryStream ms = new MemoryStream())
            using (BsonDataWriter datawriter = new BsonDataWriter(ms))
            {
                MessageExchangeStream.Serializer().Serialize(datawriter, value);
                return ms.ToArray();
            }
        }
    }
}