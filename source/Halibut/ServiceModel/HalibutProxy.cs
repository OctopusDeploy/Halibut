using System;
using System.Reflection;

using System.Threading;
using Halibut.Diagnostics;
using Halibut.Exceptions;
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
        ILog logger;
        
        public HalibutProxy(Func<RequestMessage, CancellationToken, ResponseMessage> messageRouter, Type contractType, ServiceEndPoint endPoint, ILog logger, CancellationToken cancellationToken)
            : base(contractType)
        {
            this.messageRouter = messageRouter;
            this.contractType = contractType;
            this.endPoint = endPoint;
            this.cancellationToken = cancellationToken;
            this.logger = logger;
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
#else
    public class HalibutProxy : DispatchProxy
    {
        Func<RequestMessage, CancellationToken, ResponseMessage> messageRouter;
        Type contractType;
        ServiceEndPoint endPoint;
        long callId;
        bool configured;
        CancellationToken cancellationToken;
        ILog logger;

        public void Configure(Func<RequestMessage, ResponseMessage> messageRouter, Type contractType, ServiceEndPoint endPoint,  ILog logger)
        {
            Configure((requestMessage, ct) => messageRouter(requestMessage), contractType, endPoint, logger, CancellationToken.None);
        }

        public void Configure(Func<RequestMessage, CancellationToken, ResponseMessage> messageRouter, Type contractType, ServiceEndPoint endPoint, ILog logger, CancellationToken cancellationToken)
        {
            this.messageRouter = messageRouter;
            this.contractType = contractType;
            this.endPoint = endPoint;
            this.cancellationToken = cancellationToken;
            this.configured = true;
            this.logger = logger;
        }

        protected override object Invoke(MethodInfo targetMethod, object[] args)
        {
            if (!configured)
                throw new Exception("Proxy not configured");

            var request = CreateRequest(targetMethod, args);

            var response = DispatchRequest(request);

            EnsureNotError(response);

            var result = response.Result;

            var returnType = targetMethod.ReturnType;
            if (result != null && returnType != typeof(void) && !returnType.IsInstanceOfType(result))
            {
                result = Convert.ChangeType(result, returnType);
            }

            return result;
        }

        RequestMessage CreateRequest(MethodInfo targetMethod, object[] args)
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

        ResponseMessage DispatchRequest(RequestMessage requestMessage)
        {
            return messageRouter(requestMessage, cancellationToken);
        }
#endif
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
                        Exception e = (Exception)ctor.Invoke(new object[] { error.Message, realException });
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
            catch (Exception exception) when (!(exception is HalibutClientException))
            {
                // Something went wrong trying to understand the ServerError revert back to the old behaviour of just
                // throwing a standard halibut client exception.
                logger.Write(EventType.Error, "Error {0} when processing ServerError", exception);
            }

            throw new HalibutClientException(error.Message, realException);
            
        }
    }
}