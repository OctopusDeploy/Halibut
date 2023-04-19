using System;
using System.Linq;
using System.Reflection;

using System.Threading;
using System.Threading.Tasks;
using Halibut.Transport.Protocol;

namespace Halibut.ServiceModel
{
#if HAS_REAL_PROXY
    using System.Runtime.Remoting.Messaging;
    using System.Runtime.Remoting.Proxies;
    class HalibutProxy : RealProxy
    {
        readonly Func<RequestMessage, CancellationToken, ResponseMessage> messageRouter;
        readonly Type contractType;
        readonly ServiceEndPoint endPoint;
        readonly CancellationToken cancellationToken;
        long callId;
        
        public HalibutProxy(Func<RequestMessage, CancellationToken, ResponseMessage> messageRouter, Type contractType, ServiceEndPoint endPoint, CancellationToken cancellationToken)
            : base(contractType)
        {
            this.messageRouter = messageRouter;
            this.contractType = contractType;
            this.endPoint = endPoint;
            this.cancellationToken = cancellationToken;
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

        RequestMessage CreateRequest(IMethodMessage methodCall)
        {
            var activityId = Guid.NewGuid();

            var method = ((MethodInfo) methodCall.MethodBase);
            var request = new RequestMessage
            {
                Id = contractType.Name + "::" + method.Name + "[" + Interlocked.Increment(ref callId) + "] / " + activityId,
                ActivityId = activityId,
                Destination = endPoint,
                MethodName = method.Name,
                ServiceName = contractType.Name,
                Params = methodCall.Args
            };
            return request;
        }

        ResponseMessage DispatchRequest(RequestMessage requestMessage)
        {
            return messageRouter(requestMessage, cancellationToken);
        }

        static void EnsureNotError(ResponseMessage responseMessage)
        {
            if (responseMessage == null)
                throw new HalibutClientException("No response was received from the endpoint within the allowed time.");

            if (responseMessage.Error == null)
                return;

            var realException = responseMessage.Error.Details as string;
            throw new HalibutClientException(responseMessage.Error.Message, realException);
        }
    }
#else

    public interface IMessageRouter
    {
        Task<ResponseMessage> routeMessageAsync(RequestMessage requestMessage, CancellationToken cancellationToken);
        
        ResponseMessage routeMessage(RequestMessage requestMessage, CancellationToken cancellationToken);
        
    }
    
    public class FuncPoweredMessageRouter : IMessageRouter
    {
        Func<RequestMessage, CancellationToken, ResponseMessage> routeMessageFunc;
        Func<RequestMessage, CancellationToken, Task<ResponseMessage>> routeMessageAsyncFunc;

        public FuncPoweredMessageRouter(Func<RequestMessage, CancellationToken, ResponseMessage> routeMessageFunc, Func<RequestMessage, CancellationToken, Task<ResponseMessage>> routeMessageAsyncFunc)
        {
            this.routeMessageFunc = routeMessageFunc;
            this.routeMessageAsyncFunc = routeMessageAsyncFunc;
        }

        public Task<ResponseMessage> routeMessageAsync(RequestMessage requestMessage, CancellationToken cancellationToken)
        {
            return routeMessageAsyncFunc(requestMessage, cancellationToken);
        }

        public ResponseMessage routeMessage(RequestMessage requestMessage, CancellationToken cancellationToken)
        {
            return routeMessageFunc(requestMessage, cancellationToken);
        }
        
    }
    
    public class HalibutProxy : DispatchProxyAsync
    {
        IMessageRouter messageRouter;
        Type contractType;
        ServiceEndPoint endPoint;
        long callId;
        bool configured;
        CancellationToken cancellationToken;

        public void Configure(IMessageRouter messageRouter, Type contractType, ServiceEndPoint endPoint, CancellationToken cancellationToken)
        {
            this.messageRouter = messageRouter;
            this.contractType = contractType;
            this.endPoint = endPoint;
            this.cancellationToken = cancellationToken;
            this.configured = true;
        }

        public override object Invoke(MethodInfo targetMethod, object[] args)
        {
            if (!configured)
                throw new Exception("Proxy not configured");

            var request = CreateRequest(targetMethod, contractType, args);

            var response = messageRouter.routeMessage(request, cancellationToken);;

            EnsureNotError(response);

            var result = response.Result;

            var returnType = targetMethod.ReturnType;
            if (result != null && returnType != typeof(void) && !returnType.IsInstanceOfType(result))
            {
                result = Convert.ChangeType(result, returnType);
            }

            return result;
        }

        public override Task InvokeAsync(MethodInfo method, object[] args)
        {
            throw new NotImplementedException();
        }

        public override async Task<T> InvokeAsyncT<T>(MethodInfo targetMethod, object[] args)
        {
            var contractTypeForReal = contractType;
            
            var actualMethod = contractTypeForReal.GetMethods().First(m => targetMethod.Name.Equals(m.Name + "Async"));

            // Call the sync one.
            targetMethod = actualMethod;
            
            if (!configured)
                throw new Exception("Proxy not configured");

            var request = CreateRequest(targetMethod, contractTypeForReal, args);

            var response = await messageRouter.routeMessageAsync(request, cancellationToken);;

            EnsureNotError(response);

            var result = response.Result;

            var returnType = targetMethod.ReturnType;
            if (result != null && returnType != typeof(void) && !returnType.IsInstanceOfType(result))
            {
                result = (T) Convert.ChangeType(result, returnType);
            }

            return (T) result;
        }

        RequestMessage CreateRequest(MethodInfo targetMethod, Type contractType, object[] args)
        {
            var activityId = Guid.NewGuid();

            var request = new RequestMessage
            {
                Id = contractType.Name + "::" + targetMethod.Name + "[" + Interlocked.Increment(ref callId) + "] / " + activityId,
                ActivityId = activityId,
                Destination = endPoint,
                MethodName = targetMethod.Name,
                ServiceName = contractType.Name,
                Params = args
            };
            return request;
        }

        static void EnsureNotError(ResponseMessage responseMessage)
        {
            if (responseMessage == null)
                throw new HalibutClientException("No response was received from the endpoint within the allowed time.");

            if (responseMessage.Error == null)
                return;

            var realException = responseMessage.Error.Details as string;
            throw new HalibutClientException(responseMessage.Error.Message, realException);
        }
    }
#endif
}