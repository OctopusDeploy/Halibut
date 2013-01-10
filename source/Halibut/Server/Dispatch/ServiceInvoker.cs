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

using System.Reflection;
using Halibut.Protocol;

namespace Halibut.Server.Dispatch
{
    public class ServiceInvoker : IServiceInvoker
    {
        public JsonRpcResponse Invoke(object service, JsonRpcRequest request)
        {
            var serviceType = service.GetType();
            var methodInfo = serviceType.GetMethod(request.Method);
            var args = GetArguments(request, methodInfo);

            var invoker = DelegateInvoker.CreateInvoker(service, methodInfo);
            var result = invoker.Call(args);

            return new JsonRpcResponse {Id = request.Id, Result = result};
        }

        static object[] GetArguments(JsonRpcRequest request, MethodInfo methodInfo)
        {
            var methodParams = methodInfo.GetParameters();
            var args = new object[methodParams.Length];
            for (var i = 0; i < methodParams.Length; i++)
            {
                if (i >= request.Params.Length) continue;

                var jsonArg = request.Params[i];
                args[i] = jsonArg;
            }

            return args;
        }
    }
}