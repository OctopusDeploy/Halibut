using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Halibut.Transport.Protocol
{
    public class MessageSerializerBuilder
    {
        ITypeRegistry typeRegistry;
        Action<JsonSerializerSettings> configureSerializer;

        public MessageSerializerBuilder WithTypeRegistry(ITypeRegistry typeRegistry)
        {
            this.typeRegistry = typeRegistry;
            return this;
        }

        public MessageSerializerBuilder WithSerializerSettings(Action<JsonSerializerSettings> configure)
        {
            this.configureSerializer = configure;
            return this;
        }

        public MessageSerializer Build()
        {
            var typeRegistry = this.typeRegistry ?? new TypeRegistry();

            JsonSerializer Serializer()
            {
                var settings = CreateSerializer();
                var binder = new RegisteredSerializationBinder(typeRegistry);
                settings.SerializationBinder = binder;
                configureSerializer?.Invoke(settings);
                return JsonSerializer.Create(settings);
            }

            var messageSerializer = new MessageSerializer(typeRegistry, Serializer);

            return messageSerializer;
        }

        internal static JsonSerializerSettings CreateSerializer()
        {
            var jsonSerializerSettings = new JsonSerializerSettings
            {
                Formatting = Formatting.None,
                ContractResolver = HalibutContractResolver.Instance,
                TypeNameHandling = TypeNameHandling.Auto,
                TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Simple,
                DateFormatHandling = DateFormatHandling.IsoDateFormat,
            };
            return jsonSerializerSettings;
        }
    }
}