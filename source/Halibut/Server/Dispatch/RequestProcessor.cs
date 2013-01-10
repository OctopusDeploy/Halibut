// Copyright 2012-2013 Octopus Deploy Pty. Ltd.
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//    http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.IO;
using Halibut.Diagnostics;
using Halibut.Protocol;
using Halibut.Server.ServiceModel;
using Newtonsoft.Json;
using Newtonsoft.Json.Bson;

namespace Halibut.Server.Dispatch
{
    public class RequestProcessor : IRequestProcessor
    {
        static readonly ILog Log = Logs.Server;
        readonly JsonSerializer serializer;
        readonly IServiceFactory serviceFactory;
        readonly IServiceInvoker serviceInvoker;
        readonly IServiceCatalog services;

        public RequestProcessor(JsonSerializer serializer, IServiceFactory serviceFactory, IServiceCatalog services, IServiceInvoker serviceInvoker)
        {
            this.serializer = serializer;
            this.serviceFactory = serviceFactory;
            this.services = services;
            this.serviceInvoker = serviceInvoker;
        }

        public void Execute(Stream client)
        {
            using (var reader = new BsonReader(client))
            using (var writer = new BsonWriter(client))
            {
                try
                {
                    Log.Info("Deserializing JSON-RPC request");

                    var request = serializer.Deserialize<JsonRpcRequest>(reader);
                    if (request == null) throw new ArgumentException("No JSON-RPC request was sent");

                    using (Log.BeginActivity(request.Id, request.ActivityId))
                    {
                        try
                        {
                            Log.Info("Validating request");

                            if (string.IsNullOrWhiteSpace(request.Service)) throw new ArgumentException("No service name was specified in the JSON-RPC request");
                            if (string.IsNullOrWhiteSpace(request.Method)) throw new ArgumentException("No service method was specified in the JSON-RPC request");

                            Log.InfoFormat("Resolving service {0}", request.Service);

                            var serviceType = services.GetService(request.Service);
                            if (serviceType == null)
                            {
                                throw new ArgumentException(string.Format("The service type {0} is not implemented on this server", request.Service));
                            }

                            Log.InfoFormat("Constructing service {0}", serviceType.FullName);

                            JsonRpcResponse response;
                            using (var lease = serviceFactory.CreateService(serviceType))
                            {
                                var service = lease.Service;

                                response = serviceInvoker.Invoke(service, request);
                            }

                            SendResponse(writer, response);
                        }
                        catch (Exception ex)
                        {
                            SendResponse(writer, CreateError(ex, request));
                        }
                    }
                }
                catch (Exception ex)
                {
                    SendResponse(writer, CreateError(ex, null));
                }
            }
        }

        void SendResponse(JsonWriter writer, JsonRpcResponse response)
        {
            Log.Info(response.Error != null ? "Sending error response" : "Sending successful response");
            try
            {
                serializer.Serialize(writer, response);
            }
            catch (Exception ex)
            {
                Log.Error("Unable to send response: " + ex.Message);
            }
        }

        static JsonRpcResponse CreateError(Exception ex, JsonRpcRequest request)
        {
            Log.Error(ex.Message);
            Log.Error(ex.ToString());

            return new JsonRpcResponse
            {
                Id = request == null ? null : request.Id,
                Error = new JsonRpcError {Code = ex.GetType().Name, Message = ex.Message, Data = ex.ToString()}
            };
        }
    }
}