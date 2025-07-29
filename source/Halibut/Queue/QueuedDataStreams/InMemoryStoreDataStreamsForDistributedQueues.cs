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
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Halibut.Queue.QueuedDataStreams
{
    public class InMemoryStoreDataStreamsForDistributedQueues : IStoreDataStreamsForDistributedQueues
    {
        public IDictionary<Guid, byte[]> dataStreamsStored = new Dictionary<Guid, byte[]>();
        public async Task StoreDataStreams(IReadOnlyList<DataStream> dataStreams, CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            
            foreach (var dataStream in dataStreams)
            {
                using var memoryStream = new MemoryStream();
                await dataStream.WriteData(memoryStream, cancellationToken);
                dataStreamsStored[dataStream.Id] = memoryStream.ToArray();
            }
        }

        public async Task ReHydrateDataStreams(IReadOnlyList<DataStream> dataStreams, CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            foreach (var dataStream in dataStreams)
            {
                var bytes = dataStreamsStored[dataStream.Id];
                dataStreamsStored.Remove(dataStream.Id);
                dataStream.SetWriterAsync(async (stream, ct) =>
                {
                    await stream.WriteAsync(bytes, 0, bytes.Length, ct);
                });
            }
        }
    }
}