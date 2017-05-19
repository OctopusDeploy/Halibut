using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Halibut.Portability;
using Halibut.Transport.Protocol;

namespace Halibut.ServiceModel
{
    public class ServiceInvoker : IServiceInvoker
    {
        readonly IServiceFactory factory;

        public ServiceInvoker(IServiceFactory factory)
        {
            this.factory = factory;
        }

        public async Task<ResponseMessage> Invoke(RequestMessage requestMessage)
        {
            using (var lease = factory.CreateService(requestMessage.ServiceName))
            {
                var methods = lease.Service.GetType().GetMethods().Where(m => string.Equals(m.Name, requestMessage.MethodName, StringComparison.OrdinalIgnoreCase)).ToList();
                if (methods.Count == 0)
                {
                    return ResponseMessage.FromError(requestMessage, $"Service {lease.Service.GetType().FullName}::{requestMessage.MethodName} not found");
                }

                var method = SelectMethod(methods, requestMessage);
                var args = GetArguments(requestMessage, method);
                var result = await InvokeAsync(method, lease.Service, args).ConfigureAwait(false);
                return ResponseMessage.FromResult(requestMessage, result);
            }
        }

        static async Task<object> InvokeAsync(MethodBase methodBase, object obj, params object[] parameters)
        {
            var task = (Task)methodBase.Invoke(obj, parameters);
            await task.ConfigureAwait(false);
            var resultProperty = task.GetType().GetProperty("Result");
            return resultProperty.GetValue(task);
        }

        static MethodInfo SelectMethod(IList<MethodInfo> methods, RequestMessage requestMessage)
        {
            var argumentTypes = requestMessage.Params.Select(s => s?.GetType()).ToList();

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
                    if (argType == null && paramType.IsValueType())
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