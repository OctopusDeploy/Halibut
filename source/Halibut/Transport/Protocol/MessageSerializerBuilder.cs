using System;
using Halibut.Transport.Observability;
using Newtonsoft.Json;

namespace Halibut.Transport.Protocol
{
    public class MessageSerializerBuilder
    {
        ITypeRegistry typeRegistry;
        Action<JsonSerializerSettings> configureSerializer;
        IMessageSerializerObserver messageSerializerObserver;

        public MessageSerializerBuilder WithTypeRegistry(ITypeRegistry typeRegistry)
        {
            this.typeRegistry = typeRegistry;
            return this;
        }

        public MessageSerializerBuilder WithSerializerSettings(Action<JsonSerializerSettings> configure)
        {
            configureSerializer = configure;
            return this;
        }

        public MessageSerializerBuilder WithMessageSerializerObserver(IMessageSerializerObserver messageSerializerObserver)
        {
            this.messageSerializerObserver = messageSerializerObserver;
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

            var messageSerializerObserver = this.messageSerializerObserver ?? new NoMessageSerializerObserver();

            var messageSerializer = new MessageSerializer(typeRegistry, Serializer, messageSerializerObserver);

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