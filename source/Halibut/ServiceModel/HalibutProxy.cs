using System;
using System.Reflection;

using System.Threading;
using Halibut.Transport.Protocol;

namespace Halibut.ServiceModel
{
#if HAS_REAL_PROXY
    using System.Runtime.Remoting.Messaging;
    using System.Runtime.Remoting.Proxies;
    class HalibutProxy : RealProxy
    {
        readonly Func<MessageEnvelope, CancellationToken, MessageEnvelope> messageRouter;
        readonly Type contractType;
        readonly ServiceEndPoint endPoint;
        readonly CancellationToken cancellationToken;
        long callId;
        
        public HalibutProxy(Func<MessageEnvelope, CancellationToken, MessageEnvelope> messageRouter, Type contractType, ServiceEndPoint endPoint, CancellationToken cancellationToken)
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

                var messageResponse = response.GetMessage<ResponseMessage>();
                
                EnsureNotError(messageResponse);

                var result = messageResponse.Result;

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

        MessageEnvelope CreateRequest(IMethodMessage methodCall)
        {
            var activityId = Guid.NewGuid();

            var method = ((MethodInfo) methodCall.MethodBase);
            var id = contractType.Name + "::" + method.Name + "[" + Interlocked.Increment(ref callId) + "] / " + activityId;
            
            var message = new OutgoingMessageEnvelope(id)
            {
                Message = new RequestMessage
                {
                    Id = id,
                    ActivityId = activityId,
                    Destination = endPoint,
                    MethodName = method.Name,
                    ServiceName = contractType.Name,
                    Params = methodCall.Args
                },
                Destination = endPoint
            };
            return message;
        }

        MessageEnvelope DispatchRequest(MessageEnvelope requestMessage)
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
    public class HalibutProxy : DispatchProxy
    {
        Func<MessageEnvelope, CancellationToken, MessageEnvelope> messageRouter;
        Type contractType;
        ServiceEndPoint endPoint;
        long callId;
        bool configured;
        CancellationToken cancellationToken;

        public void Configure(Func<MessageEnvelope, MessageEnvelope> messageRouter, Type contractType, ServiceEndPoint endPoint)
        {
            Configure((requestMessage, ct) => messageRouter(requestMessage), contractType, endPoint, CancellationToken.None);
        }

        public void Configure(Func<MessageEnvelope, CancellationToken, MessageEnvelope> messageRouter, Type contractType, ServiceEndPoint endPoint, CancellationToken cancellationToken)
        {
            this.messageRouter = messageRouter;
            this.contractType = contractType;
            this.endPoint = endPoint;
            this.cancellationToken = cancellationToken;
            this.configured = true;
        }

        protected override object Invoke(MethodInfo targetMethod, object[] args)
        {
            if (!configured)
                throw new Exception("Proxy not configured");

            var request = CreateRequest(targetMethod, args);

            var response = DispatchRequest(request);

            var responseMessage = response.GetMessage<ResponseMessage>();
            
            EnsureNotError(responseMessage);

            var result = responseMessage.Result;

            var returnType = targetMethod.ReturnType;
            if (result != null && returnType != typeof(void) && !returnType.IsInstanceOfType(result))
            {
                result = Convert.ChangeType(result, returnType);
            }

            return result;
        }

        MessageEnvelope CreateRequest(MethodInfo targetMethod, object[] args)
        {
            var activityId = Guid.NewGuid();
            var id = contractType.Name + "::" + targetMethod.Name + "[" + Interlocked.Increment(ref callId) + "] / " + activityId;
            var request = new OutgoingMessageEnvelope(id)
            {
                Message = new RequestMessage
                {
                    Id = id,
                    ActivityId = activityId,
                    Destination = endPoint,
                    MethodName = targetMethod.Name,
                    ServiceName = contractType.Name,
                    Params = args
                },
                Destination = endPoint
            };
            return request;
        }

        MessageEnvelope DispatchRequest(MessageEnvelope requestMessage)
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
#endif
}