#nullable enable
using System;
using System.Linq;
using System.Reflection;

namespace Halibut.ServiceModel
{
    public class AsyncCompatibilityHelper
    {
        public static MethodInfo? TryFindMatchingSyncMethod(MethodInfo asyncMethodInfo, Type syncServiceInterfaceType, bool dropHalibutProxyOptionsFromAsyncMethod)
        {
            if (!asyncMethodInfo.Name.EndsWith("Async"))
            {
                throw new Exception($"Async methods must end in 'Async': " + asyncMethodInfo);
            }
                
            var syncMethodName = SyncMethodName(asyncMethodInfo);
            
            var paramsToLookFor = asyncMethodInfo.GetParameters().Select(p => p.ParameterType).ToArray();
            if (dropHalibutProxyOptionsFromAsyncMethod && paramsToLookFor.Length > 0 && paramsToLookFor.Last() == typeof(HalibutProxyRequestOptions))
            {
                 paramsToLookFor = paramsToLookFor.Take(paramsToLookFor.Length - 1).ToArray();
            }
            return syncServiceInterfaceType.GetMethod(syncMethodName, paramsToLookFor);
        }

        static string SyncMethodName(MethodInfo asyncMethodInfo)
        {
            return asyncMethodInfo.Name.Substring(0, asyncMethodInfo.Name.Length - "Async".Length);
        }

        public static MethodInfo FindMatchingSyncMethod(MethodInfo asyncMethodInfo, Type syncServiceInterfaceType, bool dropHalibutProxyOptionsFromAsyncMethod)
        {
            var syncMethodInfo = TryFindMatchingSyncMethod(asyncMethodInfo, syncServiceInterfaceType, dropHalibutProxyOptionsFromAsyncMethod);
            if (syncMethodInfo == null)
            {
                throw new Exception( $"Could not find an async counterpart for: '{asyncMethodInfo}' we looked for {SyncMethodName(asyncMethodInfo)} but could " +
                                     $"not find a match in {syncServiceInterfaceType.FullName}");
            }

            return syncMethodInfo;
        }

        public static string GetAsyncMethodName(string methodName)
        {
            return methodName + "Async";
        }
    }
}
