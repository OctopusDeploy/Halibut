using System;
using Halibut.Diagnostics;
using Halibut.Transport.Observability;
using Newtonsoft.Json;

namespace Halibut.Transport.Protocol
{
    public class MessageSerializerBuilder
    {
        readonly ILogFactory logFactory;
        ITypeRegistry typeRegistry;
        Action<JsonSerializerSettings> configureSerializer;
        IMessageSerializerObserver messageSerializerObserver;
        // Initial prod telemetry showed quite large read values (> 17M). But a decent number were below 64K.
        // This number is also below what .NET uses to put objects on the LOH (threshold is 85K)
        long readIntoMemoryLimitBytes = 1024L * 64L;
        // Initial prod telemetry indicated values < 7K would be fine. But to be safe, 64K to future proof, and stay below the LOH threshold of 85K.
        long writeIntoMemoryLimitBytes = 1024L * 64L;

        public MessageSerializerBuilder(ILogFactory logFactory)
        {
            this.logFactory = logFactory;
        }

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

        public MessageSerializerBuilder WithAsyncMemoryLimits(long readIntoMemoryLimitBytes, long writeIntoMemoryLimitBytes)
        {
            this.readIntoMemoryLimitBytes = readIntoMemoryLimitBytes;
            this.writeIntoMemoryLimitBytes = writeIntoMemoryLimitBytes;
            return this;
        }

        public MessageSerializer Build()
        {
            var typeRegistry = this.typeRegistry ?? new TypeRegistry();

            [Obsolete]
            JsonSerializer Serializer()
            {
                var settings = CreateSerializer();
                var binder = new RegisteredSerializationBinder(typeRegistry);
                settings.SerializationBinder = binder;
                configureSerializer?.Invoke(settings);
                return JsonSerializer.Create(settings);
            }

            StreamCapturingJsonSerializer StreamCapturingSerializer()
            {
                var settings = CreateSerializer();
                var binder = new RegisteredSerializationBinder(typeRegistry);
                settings.SerializationBinder = binder;
                configureSerializer?.Invoke(settings);
                return new StreamCapturingJsonSerializer(settings);
            }

            var messageSerializerObserver = this.messageSerializerObserver ?? new NoMessageSerializerObserver();

            var messageSerializer = new MessageSerializer(
                typeRegistry, 
#pragma warning disable CS0612
                Serializer, 
#pragma warning restore CS0612
                StreamCapturingSerializer, 
                messageSerializerObserver,
                readIntoMemoryLimitBytes,
                writeIntoMemoryLimitBytes,
                logFactory);

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