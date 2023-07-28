#nullable enable
using System;
using System.Linq;
using System.Reflection;

namespace Halibut.ServiceModel
{
    public class AsyncCompatibilityHelper
    {
        public static MethodInfo? TryFindMatchingSyncMethod(MethodInfo asyncMethodInfo, Type syncServiceInterfaceType)
        {
            if (!asyncMethodInfo.Name.EndsWith("Async"))
            {
                throw new Exception($"Async methods must end in 'Async': " + asyncMethodInfo);
            }
                
            var syncMethodName = SyncMethodName(asyncMethodInfo);
            return syncServiceInterfaceType.GetMethod(syncMethodName, asyncMethodInfo.GetParameters().Select(p => p.ParameterType).ToArray());
        }

        static string SyncMethodName(MethodInfo asyncMethodInfo)
        {
            return asyncMethodInfo.Name.Substring(0, asyncMethodInfo.Name.Length - "Async".Length);
        }

        public static MethodInfo FindMatchingSyncMethod(MethodInfo asyncMethodInfo, Type syncServiceInterfaceType)
        {
            var syncMethodInfo = TryFindMatchingSyncMethod(asyncMethodInfo, syncServiceInterfaceType);
            if (syncMethodInfo == null)
            {
                throw new Exception( $"Could not find an async counterpart for: '{asyncMethodInfo}' we looked for {SyncMethodName(asyncMethodInfo)} but could " +
                                     $"not find a match in {syncServiceInterfaceType.FullName}");
            }

            return syncMethodInfo;
        }
    }
}