// Copyright 2012-2013 Octopus Deploy Pty. Ltd.
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//   http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

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