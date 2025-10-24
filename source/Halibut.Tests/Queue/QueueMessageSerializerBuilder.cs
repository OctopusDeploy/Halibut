using System;
using System.Collections.Generic;
using Halibut.Diagnostics;
using Halibut.Queue;
using Halibut.Queue.MessageStreamWrapping;
using Halibut.Transport.Protocol;
using Newtonsoft.Json;

namespace Halibut.Tests.Queue
{
    public class QueueMessageSerializerBuilder
    {
        ITypeRegistry? typeRegistry;
        Action<JsonSerializerSettings>? configureSerializer;

        MessageStreamWrappers messageStreamWrappers = new MessageStreamWrappers(new List<IMessageStreamWrapper>());

        public QueueMessageSerializerBuilder WithTypeRegistry(ITypeRegistry typeRegistry)
        {
            this.typeRegistry = typeRegistry;
            return this;
        }

        public QueueMessageSerializerBuilder WithMessageStreamWrappers(MessageStreamWrappers messageStreamWrappers)
        {
            this.messageStreamWrappers = messageStreamWrappers;
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
            RegisteredSerializationBinder.AddProtocolTypesToTypeRegistry(typeRegistry);

            StreamCapturingJsonSerializer StreamCapturingSerializer()
            {
                var settings = MessageSerializerBuilder.CreateSerializer();
                var binder = new RegisteredSerializationBinder(typeRegistry);
                settings.SerializationBinder = binder;
                configureSerializer?.Invoke(settings);
                return new StreamCapturingJsonSerializer(settings);
            }

            return new QueueMessageSerializer(StreamCapturingSerializer, messageStreamWrappers);
        }
    }
}