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
using System.Threading;
using System.Threading.Tasks;
using Halibut.Queue.Redis;
using Halibut.Transport.Protocol;

namespace Halibut.Tests.Queue.Redis.Utils
{
    public static class MessageReaderWriterExtensionsMethods
    {
        public static IMessageReaderWriter ThrowsOnReadResponse(this IMessageReaderWriter messageReaderWriter, Func<Exception> exceptionFactory)
        {
            return new MessageReaderWriterThatThrowsWhenReadingResponse(messageReaderWriter, exceptionFactory);
        }

        public static IMessageReaderWriter ThrowsOnPrepareRequest(this IMessageReaderWriter messageReaderWriter, Func<Exception> exception)
        {
            return new MessageReaderWriterThatThrowsOnPrepareRequest(messageReaderWriter, exception);
        }
}

    class MessageReaderWriterWithVirtualMethods : IMessageReaderWriter
    {
        readonly IMessageReaderWriter messageReaderWriter;

        public MessageReaderWriterWithVirtualMethods(IMessageReaderWriter messageReaderWriter)
        {
            this.messageReaderWriter = messageReaderWriter;
        }

        public virtual Task<string> PrepareRequest(RequestMessage request, CancellationToken cancellationToken)
        {
            return messageReaderWriter.PrepareRequest(request, cancellationToken);
        }

        public virtual Task<RequestMessage> ReadRequest(string jsonRequest, CancellationToken cancellationToken)
        {
            return messageReaderWriter.ReadRequest(jsonRequest, cancellationToken);
        }

        public virtual Task<string> PrepareResponse(ResponseMessage response, CancellationToken cancellationToken)
        {
            return messageReaderWriter.PrepareResponse(response, cancellationToken);
        }

        public virtual Task<ResponseMessage> ReadResponse(string jsonResponse, CancellationToken cancellationToken)
        {
            return messageReaderWriter.ReadResponse(jsonResponse, cancellationToken);
        }
    }

    class MessageReaderWriterThatThrowsWhenReadingResponse : MessageReaderWriterWithVirtualMethods
    {
        readonly Func<Exception> exception;

        public MessageReaderWriterThatThrowsWhenReadingResponse(IMessageReaderWriter messageReaderWriter, Func<Exception> exception) : base(messageReaderWriter)
        {
            this.exception = exception;
        }

        public override Task<ResponseMessage> ReadResponse(string jsonResponse, CancellationToken cancellationToken)
        {
            throw exception();
        }
    }
    
    class MessageReaderWriterThatThrowsOnPrepareRequest : MessageReaderWriterWithVirtualMethods
    {
        readonly Func<Exception> exception;

        public MessageReaderWriterThatThrowsOnPrepareRequest(IMessageReaderWriter messageReaderWriter, Func<Exception> exception) : base(messageReaderWriter)
        {
            this.exception = exception;
        }

        public override Task<string> PrepareRequest(RequestMessage request, CancellationToken cancellationToken)
        {
            throw exception();
        }
    }
}