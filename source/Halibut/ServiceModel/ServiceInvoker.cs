using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Exceptions;
using Halibut.Portability;
using Halibut.Transport.Protocol;
using Halibut.Util;

namespace Halibut.ServiceModel
{
    public class ServiceInvoker : IServiceInvoker
    {
        readonly IServiceFactory factory;

        public ServiceInvoker(IServiceFactory factory)
        {
            this.factory = factory;
        }

        public async Task<ResponseMessage> InvokeAsync(RequestMessage requestMessage)
        {
            using var lease = factory.CreateService(requestMessage.ServiceName);

            var asyncMethodName = AsyncCompatibilityHelper.GetAsyncMethodName(requestMessage.MethodName);
            var asyncMethods = lease.Service.GetType().GetHalibutServiceMethods().Where(m => string.Equals(m.Name, asyncMethodName, StringComparison.OrdinalIgnoreCase)).ToList();

            if (asyncMethods.Count != 0)
            {
                var asyncMethod = SelectAsyncMethod(asyncMethods, requestMessage);
                var asyncArgs = GetArguments(requestMessage, asyncMethod);
                asyncArgs[asyncArgs.Length - 1] = CancellationToken.None;
                var asyncResult = await InvokeAsyncMethod(asyncMethod, lease.Service, asyncArgs);
                return ResponseMessage.FromResult(requestMessage, asyncResult);
            }

            throw new MethodNotFoundHalibutClientException(string.Format("Method {0}::{1} not found", lease.Service.GetType().FullName, asyncMethodName));
        }

        static MethodInfo SelectAsyncMethod(IList<MethodInfo> methods, RequestMessage requestMessage)
        {
            var argumentTypes = requestMessage.Params?.Select(s => s?.GetType()).ToList() ?? new List<Type?>();

            var matches = new List<MethodInfo>();

            foreach (var method in methods)
            {
                var parameters = method.GetParameters();
                if (parameters.Any() && parameters.Last().ParameterType == typeof(CancellationToken))
                {
                    parameters = parameters.Take(parameters.Length - 1).ToArray();
                }
                else
                {
                    continue;
                }

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

            throw new AmbiguousMethodMatchHalibutClientException(message.ToString(), new AmbiguousMatchException(message.ToString()));
        }

        async Task<object?> InvokeAsyncMethod(MethodInfo method, object obj, params object[] parameters)
        {
            var returnType = method.ReturnType;

            // If the return type is not Task or Task<T>, then shrug and explode!
            if (!typeof(Task).IsAssignableFrom(returnType))
            {
                throw new InvalidOperationException($"InvokeAsyncMethod cannot be called on a method that does not return Task or Task<T> ({returnType.Name})");
            }

            var task = (Task)method.Invoke(obj, parameters)!;
            try
            {
                await task.ConfigureAwait(false);
            }
            catch (Exception e)
            {
                throw new TargetInvocationException(e);
            }

            if (returnType == typeof(Task))
            {
                // If async method has a 'void' return type (i.e. Task with no generic params),
                // then return null to match behaviour of MethodInfo.Invoke
                return null;
            }

            // If the return type is not Task, we assume it is Task<T>
            var resultType = returnType.GetGenericArguments().First();
            var resultProperty = typeof(Task<>).MakeGenericType(resultType).GetProperty("Result")!;
            return resultProperty.GetValue(task);
        }

        public static object[] GetArguments(RequestMessage requestMessage, MethodInfo methodInfo)
        {
            var methodParams = methodInfo.GetParameters();
            var args = new object[methodParams.Length];
            var requestMessageParams = requestMessage.Params ?? Array.Empty<ParameterInfo>();
            for (var i = 0; i < methodParams.Length; i++)
            {
                if (i >= requestMessageParams.Length) continue;

                var jsonArg = requestMessageParams[i];
                args[i] = jsonArg;
            }

            return args;
        }
    }
}
