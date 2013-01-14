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
using System.Reflection;
using System.Runtime.Remoting.Messaging;
using System.Runtime.Remoting.Proxies;
using System.Threading;
using Halibut.Protocol;

namespace Halibut.Client
{
    class HalibutProxy : RealProxy
    {
        readonly Type contractType;
        readonly ServiceEndPoint endPoint;
        readonly IHalibutClient rpcClient;
        long callId;

        public HalibutProxy(IHalibutClient rpcClient, Type contractType, ServiceEndPoint endPoint) : base(contractType)
        {
            this.rpcClient = rpcClient;
            this.contractType = contractType;
            this.endPoint = endPoint;
        }

        public override IMessage Invoke(IMessage msg)
        {
            var methodCall = msg as IMethodCallMessage;
            if (methodCall == null)
                throw new NotSupportedException("The message type " + msg + " is not supported.");

            try
            {
                var request = CreateRequest(methodCall);

                var response = DispatchRequest(request);

                EnsureNotError(response);

                var result = response.Result;

                var returnType = ((MethodInfo) methodCall.MethodBase).ReturnType;
                if (result != null && returnType != typeof (void) && !returnType.IsAssignableFrom(result.GetType()))
                {
                    result = Convert.ChangeType(result, returnType);
                }

                return new ReturnMessage(result, null, 0, null, methodCall);
            }
            catch (Exception ex)
            {
                return new ReturnMessage(ex, methodCall);
            }
        }

        JsonRpcRequest CreateRequest(IMethodMessage methodCall)
        {
            var activityId = Guid.NewGuid();

            var method = ((MethodInfo) methodCall.MethodBase);
            var request = new JsonRpcRequest
                          {
                              Id = contractType.Name + "::" + method.Name + "[" + Interlocked.Increment(ref callId) + "] / " + activityId,
                              ActivityId = activityId,
                              Service = contractType.Name,
                              Method = method.Name,
                              Params = methodCall.Args
                          };
            return request;
        }

        JsonRpcResponse DispatchRequest(JsonRpcRequest request)
        {
            return rpcClient.Post(endPoint, request);
        }

        static void EnsureNotError(JsonRpcResponse response)
        {
            if (response.Error == null)
                return;

            var realException = response.Error.Data as string;
            throw new JsonRpcException(response.Error.Message, realException);
        }
    }
}