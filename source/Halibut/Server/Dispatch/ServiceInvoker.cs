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
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Halibut.Protocol;

namespace Halibut.Server.Dispatch
{
    public class ServiceInvoker : IServiceInvoker
    {
        #region IServiceInvoker Members

        public JsonRpcResponse Invoke(object service, JsonRpcRequest request)
        {
            Type serviceType = service.GetType();
            var methods = serviceType.GetMethods().Where(m => string.Equals(m.Name, request.Method, StringComparison.OrdinalIgnoreCase)).ToList();
            var method = SelectMethod(methods, request);

            object[] args = GetArguments(request, method);

            var result = method.Invoke(service, args);

            return new JsonRpcResponse {Id = request.Id, Result = result};
        }

        MethodInfo SelectMethod(IEnumerable<MethodInfo> methods, JsonRpcRequest request)
        {
            var argumentTypes = request.Params.Select(s => s == null ? null : s.GetType()).ToList();

            var matches = new List<MethodInfo>();

            foreach (var method in methods)
            {
                var parameters = method.GetParameters();
                if (parameters.Length != argumentTypes.Count)
                {
                    continue;
                }

                var isMatch = true;
                for (var i = 0; i < parameters.Length; i++)
                {
                    var paramType = parameters[i].ParameterType;
                    var argType = argumentTypes[i];
                    if (argType == null && paramType.IsValueType)
                    {
                        isMatch = false;
                        break;
                    }

                    if (argType != null && !paramType.IsAssignableFrom(argType))
                    {
                        isMatch = false;
                        break;
                    } 
                }

                if (isMatch)
                {
                    matches.Add(method);
                }
            }

            if (matches.Count == 1)
                return matches[0];

            var message = new StringBuilder("More than one match for the service method was found. Candidates were:").AppendLine();
            foreach (var match in matches)
            {
                message.AppendLine(" - " + match);
            }

            message.AppendLine("The request arguments were:");
            message.AppendLine(string.Join(", ", argumentTypes.Select(t => t == null ? "<null>" : t.Name)));

            throw new AmbiguousMatchException(message.ToString());
        }

        #endregion

        static object[] GetArguments(JsonRpcRequest request, MethodInfo methodInfo)
        {
            ParameterInfo[] methodParams = methodInfo.GetParameters();
            var args = new object[methodParams.Length];
            for (int i = 0; i < methodParams.Length; i++)
            {
                if (i >= request.Params.Length) continue;

                object jsonArg = request.Params[i];
                args[i] = jsonArg;
            }

            return args;
        }
    }
}