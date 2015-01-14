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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Halibut.Client;
using Halibut.Protocol;
using Halibut.Server.ServiceModel;

namespace Halibut.Server.Dispatch
{
    //public class RouteTable
    //{
    //    readonly ConcurrentDictionary<Uri, ServiceEndPoint> routes = new ConcurrentDictionary<Uri, ServiceEndPoint>();

    //    public void AddRoute(ServiceEndPoint to, ServiceEndPoint via)
    //    {
            
    //    }

    //    public bool ShouldRoute(RequestMessage request)
    //    {
            
    //    }

    //    public RequestMessage CreateRoutedMessage(RequestMessage request)
    //    {
            
    //    }
    //}

    public class ServiceInvoker : IServiceInvoker
    {
        readonly IServiceFactory factory;

        public ServiceInvoker(IServiceFactory factory)
        {
            this.factory = factory;
        }

        public ResponseMessage Invoke(RequestMessage requestMessage)
        {
            using (var lease = factory.CreateService(requestMessage.ServiceName))
            {
                var methods = lease.Service.GetType().GetMethods().Where(m => string.Equals(m.Name, requestMessage.MethodName, StringComparison.OrdinalIgnoreCase)).ToList();
                if (methods.Count == 0)
                {
                    return new ResponseMessage { Id = requestMessage.Id, Error = new ServerError() { Message = string.Format("Service {0}::{1} not found", lease.Service.GetType().FullName, requestMessage.MethodName) } };
                }

                var method = SelectMethod(methods, requestMessage);

                var args = GetArguments(requestMessage, method);

                var result = method.Invoke(lease.Service, args);

                return new ResponseMessage { Id = requestMessage.Id, Result = result };
            }
        }

        static MethodInfo SelectMethod(IList<MethodInfo> methods, RequestMessage requestMessage)
        {
            var argumentTypes = requestMessage.Params.Select(s => s == null ? null : s.GetType()).ToList();

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

            var message = new StringBuilder();
            if (matches.Count > 1)
            {
                message.AppendLine("More than one possible match for the requested service method was found given the argument types. The matches were:");

                foreach (var match in matches)
                {
                    message.AppendLine(" - " + match);
                }
            }
            else
            {
                message.AppendLine("Could not decide which candidate to call out of the following methods:");

                foreach (var match in methods)
                {
                    message.AppendLine(" - " + match);
                }
            }

            message.AppendLine("The request arguments were:");
            message.AppendLine(string.Join(", ", argumentTypes.Select(t => t == null ? "<null>" : t.Name)));

            throw new AmbiguousMatchException(message.ToString());
        }

        static object[] GetArguments(RequestMessage requestMessage, MethodInfo methodInfo)
        {
            var methodParams = methodInfo.GetParameters();
            var args = new object[methodParams.Length];
            for (var i = 0; i < methodParams.Length; i++)
            {
                if (i >= requestMessage.Params.Length) continue;

                var jsonArg = requestMessage.Params[i];
                args[i] = jsonArg;
            }

            return args;
        }
    }
}