using System;
using Halibut.Diagnostics;
using Halibut.Queue;
using Halibut.Transport.Protocol;
using Newtonsoft.Json;

namespace Halibut.Tests.Queue
{
    public class QueueMessageSerializerBuilder
    {
        ITypeRegistry? typeRegistry;
        Action<JsonSerializerSettings>? configureSerializer;

        public QueueMessageSerializerBuilder WithTypeRegistry(ITypeRegistry typeRegistry)
        {
            this.typeRegistry = typeRegistry;
            return this;
        }

        public QueueMessageSerializerBuilder WithSerializerSettings(Action<JsonSerializerSettings> configure)
        {
            configureSerializer = configure;
            return this;
        }

        public QueueMessageSerializer Build()
        {
            var typeRegistry = this.typeRegistry ?? new TypeRegistry();

            StreamCapturingJsonSerializer StreamCapturingSerializer()
            {
                var settings = MessageSerializerBuilder.CreateSerializer();
                var binder = new RegisteredSerializationBinder(typeRegistry);
                settings.SerializationBinder = binder;
                configureSerializer?.Invoke(settings);
                return new StreamCapturingJsonSerializer(settings);
            }

            return new QueueMessageSerializer(StreamCapturingSerializer);
        }
    }
}