using System;
using System.Reflection;
using System.Runtime.Remoting.Messaging;
using System.Runtime.Remoting.Proxies;
using System.Threading;
using Halibut.Transport.Protocol;

namespace Halibut.ServiceModel
{
    class HalibutProxy : RealProxy
    {
        readonly Func<RequestMessage, ResponseMessage> messageRouter;
        readonly Type contractType;
        readonly ServiceEndPoint endPoint;
        long callId;

        public HalibutProxy(Func<RequestMessage, ResponseMessage> messageRouter, Type contractType, ServiceEndPoint endPoint)
            : base(contractType)
        {
            this.messageRouter = messageRouter;
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
            return messageRouter(requestMessage);
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
}