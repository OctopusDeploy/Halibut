using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Halibut.Transport.Protocol
{
    public class MessageSerializerBuilder
    {
        ITypeRegistry typeRegistry;
        Func<JsonSerializer> createSerializer;

        public MessageSerializerBuilder WithTypeRegistry(ITypeRegistry typeRegistry)
        {
            this.typeRegistry = typeRegistry;
            return this;
        }

        public MessageSerializerBuilder WithSerializerBuilder(Func<JsonSerializer> createSerializer)
        {
            this.createSerializer = createSerializer;
            return this;
        }

        public MessageSerializer Build()
        {
            var typeRegistry = this.typeRegistry ?? new TypeRegistry();
            var createSerializer = this.createSerializer ?? (() =>
            {
                var binder = new RegisteredSerializationBinder(typeRegistry);
                return CreateSerializer(binder);
            });
            var messageSerializer = new MessageSerializer(typeRegistry, createSerializer);

            return messageSerializer;
        }

        internal static JsonSerializer CreateSerializer(ISerializationBinder binder)
        {
            var jsonSerializer = JsonSerializer.Create(new JsonSerializerSettings
            {
                Formatting = Formatting.None,
                ContractResolver = HalibutContractResolver.Instance,
                TypeNameHandling = TypeNameHandling.Auto,
                TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Simple,
                DateFormatHandling = DateFormatHandling.IsoDateFormat,
                SerializationBinder = binder
            });

            return jsonSerializer;
        }
    }
}