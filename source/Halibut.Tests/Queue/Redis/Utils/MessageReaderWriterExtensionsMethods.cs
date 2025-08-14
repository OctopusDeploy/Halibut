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
    }

    class MessageReaderWriterThatThrowsWhenReadingResponse : IMessageReaderWriter
    {
        readonly IMessageReaderWriter messageReaderWriter;
        readonly Func<Exception> exception;

        public MessageReaderWriterThatThrowsWhenReadingResponse(IMessageReaderWriter messageReaderWriter, Func<Exception> exception)
        {
            this.messageReaderWriter = messageReaderWriter;
            this.exception = exception;
        }

        public Task<string> PrepareRequest(RequestMessage request, CancellationToken cancellationToken)
        {
            return messageReaderWriter.PrepareRequest(request, cancellationToken);
        }

        public Task<RequestMessage> ReadRequest(string jsonRequest, CancellationToken cancellationToken)
        {
            return messageReaderWriter.ReadRequest(jsonRequest, cancellationToken);
        }

        public Task<string> PrepareResponse(ResponseMessage response, CancellationToken cancellationToken)
        {
            return messageReaderWriter.PrepareResponse(response, cancellationToken);
        }

        public Task<ResponseMessage> ReadResponse(string jsonResponse, CancellationToken cancellationToken)
        {
            throw exception();
        }
    }
}