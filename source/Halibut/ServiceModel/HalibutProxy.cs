using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Logging.LogProviders;
using Halibut.Transport.Protocol;

namespace Halibut.ServiceModel
{
    public class HalibutProxy : DispatchProxyAsync
    {
        Func<RequestMessage, Task<ResponseMessage>> messageRouter;
        Type contractType;
        ServiceEndPoint endPoint;
        long callId;
        bool configured;

        public void Configure(Func<RequestMessage, Task<ResponseMessage>> messageRouter, Type contractType, ServiceEndPoint endPoint)
        {
            this.messageRouter = messageRouter;
            this.contractType = contractType;
            this.endPoint = endPoint;
            this.configured = true;
        }

        public override object Invoke(MethodInfo targetMethod, object[] args)
        {
            return new object();
        }

        public override Task InvokeAsync(MethodInfo targetMethod, object[] args)
        {
            return InvokeInternal(targetMethod, args);
        }

        public override async Task<T> InvokeAsyncT<T>(MethodInfo targetMethod, object[] args)
        {
            var response = await InvokeInternal(targetMethod, args).ConfigureAwait(false);
            var rr = response.Result.GetType().Name;

            Console.Out.WriteLine(rr);
            var result = await ((Task<T>)response.Result).ConfigureAwait(false);

            //var returnType = targetMethod.ReturnType;
            //if (result != null && returnType != typeof(void) && !returnType.IsInstanceOfType(result))
            //{
            //    result = Convert.ChangeType(result, returnType);
            //}

            return result;
        }

        async Task<ResponseMessage> InvokeInternal(MethodInfo targetMethod, object[] args)
        {
            if (!configured)
            {
                throw new Exception("Proxy not configured");
            }

            var request = CreateRequest(targetMethod, args);

            var response = await DispatchRequest(request).ConfigureAwait(false);

            EnsureNotError(response);

            return response;
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

        Task<ResponseMessage> DispatchRequest(RequestMessage requestMessage)
        {
            return messageRouter(requestMessage);
        }

        static void EnsureNotError(ResponseMessage responseMessage)
        {
            if (responseMessage == null)
                throw new HalibutClientException("No response was received from the endpoint within the allowed time.");

            if (responseMessage.Error == null)
                return;

            var realException = responseMessage.Error.Details;
            throw new HalibutClientException(responseMessage.Error.Message, realException);
        }
    }
}