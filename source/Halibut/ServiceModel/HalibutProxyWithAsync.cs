using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Diagnostics;
using Halibut.Exceptions;
using Halibut.Transport.Protocol;

namespace Halibut.ServiceModel
{
    public delegate Task<ResponseMessage> MessageRouter(RequestMessage request, MethodInfo serviceMethod, CancellationToken cancellationToken);

    public class HalibutProxyWithAsync : DispatchProxyAsync
    {
        MessageRouter messageRouter;
        Type contractType;
        ServiceEndPoint endPoint;
        long callId;
        bool configured;
        ILog logger;

        public void Configure(
            MessageRouter messageRouter, 
            Type contractType, 
            ServiceEndPoint endPoint,
            ILog logger)
        {
            this.messageRouter = messageRouter;
            this.contractType = contractType;
            this.endPoint = endPoint;
            this.configured = true;
            this.logger = logger;
        }

        public override object Invoke(MethodInfo targetMethod, object[] args)
        {
            throw new NotSupportedException($"Synchronous calls cannot be made with the {nameof(HalibutProxyWithAsync)}");
        }

        public override async Task InvokeAsync(MethodInfo asyncMethod, object[] args)
        {
            await MakeRpcCall(asyncMethod, args);
        }

        public override async Task<T> InvokeAsyncT<T>(MethodInfo asyncMethod, object[] args)
        {
            var (serviceMethod, result) = await MakeRpcCall(asyncMethod, args);

            var returnType = serviceMethod.ReturnType;
            if (result != null && returnType != typeof(void) && !returnType.IsInstanceOfType(result))
            {
                result = (T)Convert.ChangeType(result, returnType);
            }

            return (T)result;
        }

        async Task<(MethodInfo, object)> MakeRpcCall(MethodInfo asyncMethod, object[] args)
        {
            var serviceMethod = AsyncCompatibilityHelper.FindMatchingSyncMethod(asyncMethod, contractType, true);

            if (!configured)
                throw new Exception("Proxy not configured");

            var trimmedArgsAndHalibutProxyRequestOptions = TrimOffHalibutProxyRequestOptions(args);
            args = trimmedArgsAndHalibutProxyRequestOptions.args;
            var halibutProxyRequestOptions = trimmedArgsAndHalibutProxyRequestOptions.halibutProxyRequestOptions;

            var request = CreateRequest(asyncMethod, serviceMethod, args);

            var response = await messageRouter(request, serviceMethod, halibutProxyRequestOptions?.RequestCancellationToken ?? CancellationToken.None);

            EnsureNotError(response);
            
            return (serviceMethod, response.Result);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="asyncMethod">The async client method called, used in the call id.</param>
        /// <param name="targetMethod">The method to actually invoke</param>
        /// <param name="args"></param>
        /// <returns></returns>
        RequestMessage CreateRequest(MethodInfo asyncMethod, MethodInfo targetMethod, object[] args)
        {
            var activityId = Guid.NewGuid();

            var request = new RequestMessage
            {
                Id = contractType.Name + "::" + asyncMethod.Name + "[" + Interlocked.Increment(ref callId) + "] / " + activityId,
                ActivityId = activityId,
                Destination = endPoint,
                MethodName = targetMethod.Name,
                ServiceName = contractType.Name,
                Params = args
            };
            return request;
        }
        
        void EnsureNotError(ResponseMessage responseMessage)
        {
            if (responseMessage == null)
                throw new HalibutClientException("No response was received from the endpoint within the allowed time.");

            if (responseMessage.Error == null)
                return;

            ThrowExceptionFromReceivedError(responseMessage.Error, logger);
        }

        internal static void ThrowExceptionFromReceivedError(ServerError error, ILog logger)
        {
            var realException = error.Details as string;

            try
            {
                if (!string.IsNullOrEmpty(error.HalibutErrorType))
                {
                    var theType = Type.GetType(error.HalibutErrorType);
                    if (theType != null && theType != typeof(HalibutClientException))
                    {
                        var ctor = theType.GetConstructor(new[] { typeof(string), typeof(string) });
                        var e = (Exception)ctor.Invoke(new object[] { error.Message, realException });
                        throw e;
                    }
                }

                if (error.Message.StartsWith("Service not found: "))
                {
                    throw new ServiceNotFoundHalibutClientException(error.Message, realException);
                }

                if (error.Message.StartsWith("Service ") && error.Message.EndsWith(" not found"))
                {
                    throw new MethodNotFoundHalibutClientException(error.Message, realException);
                }

                if (error.Details.StartsWith("System.Reflection.AmbiguousMatchException: "))
                {
                    throw new AmbiguousMethodMatchHalibutClientException(error.Message, realException);
                }

                if (error.Details.StartsWith("System.Reflection.TargetInvocationException: Exception has been thrown by the target of an invocation."))
                {
                    throw new ServiceInvocationHalibutClientException(error.Message, realException);
                }

            }
            catch (Exception exception) when (exception is not HalibutClientException && exception is not RequestCancelledException)
            {
                // Something went wrong trying to understand the ServerError revert back to the old behaviour of just
                // throwing a standard halibut client exception.
                logger.Write(EventType.Error, "Error {0} when processing ServerError", exception);
            }

            throw new HalibutClientException(error.Message, realException);

        }

        internal static (object[] args, HalibutProxyRequestOptions halibutProxyRequestOptions) TrimOffHalibutProxyRequestOptions(object[] args)
        {
            if (args.Length == 0) return (args, null);
            object last = args.Last();
            if (last is not HalibutProxyRequestOptions) return (args, null);

            args = args.Take(args.Length - 1).ToArray();

            return (args, (HalibutProxyRequestOptions) last);
        }
    }
}