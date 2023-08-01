using System;
using System.Reflection;
using System.Threading.Tasks;
using Halibut.ServiceModel;

namespace Halibut.Tests.TestServices.AsyncSyncCompat
{
    /// <summary>
    ///     Given a Sync Proxy e.g. halibutRunTime.CrateClient
    ///     <INotAsyncAtAllService>
    ///         ();
    ///         And converts it to a async version of that service.
    /// </summary>
    public class AdaptSyncProxyToAsyncProxy : DispatchProxyAsync
    {
        object syncHalibutProxy;
        Type syncHalubutProxyType;

        public void Configure(object syncHalibutProxy, Type syncHalubutProxyType)
        {
            this.syncHalibutProxy = syncHalibutProxy;
            this.syncHalubutProxyType = syncHalubutProxyType;
        }

        public override object Invoke(MethodInfo methodInfo, object[] args)
        {
            throw new NotImplementedException();
        }

        MethodInfo? GetSyncMethod(MethodInfo asyncMethodInfo)
        {
            return AsyncCompatibilityHelper.FindMatchingSyncMethod(asyncMethodInfo, syncHalubutProxyType);
        }

        public override async Task InvokeAsync(MethodInfo asyncMethodInfo, object[] args)
        {
            await Task.CompletedTask;
            var syncMethod = GetSyncMethod(asyncMethodInfo);
            try
            {
                syncMethod.Invoke(syncHalibutProxy, args);
            }
            catch (TargetInvocationException e)
            {
                throw e.InnerException!;
            }
        }

        public override Task<T> InvokeAsyncT<T>(MethodInfo asyncMethodInfo, object[] args)
        {
            var syncMethod = GetSyncMethod(asyncMethodInfo);

            try
            {
                var result = (T) syncMethod.Invoke(syncHalibutProxy, args);
                return Task.FromResult(result);
            }
            catch (TargetInvocationException e)
            {
                throw e.InnerException!;
            }
        }
    }
}