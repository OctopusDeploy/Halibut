using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Halibut.Tests.TestServices.AsyncSyncCompat
{
    public class ServiceInterfaceInspector
    {
        public void EnsureAllMethodsAreAsync<T>()
        {
            foreach (var methodInfo in typeof(T).GetMethods())
            {
                if (!IsMethodAsync(methodInfo))
                {
                    throw new Exception($"Not all methods on {typeof(T)} are async, e.g. {methodInfo}");
                }
            }
        }

        static bool IsMethodAsync(MethodInfo methodInfo)
        {
            return typeof(Task).IsAssignableFrom(methodInfo.ReturnType);
        }

        public void EnsureServiceTypeAndClientServiceTypeHaveMatchingMethods<TService, TClientService>()
        {
            var serviceType = typeof(TService); 
            foreach (var methodInfo in typeof(TClientService).GetMethods())
            {
                string nameToSearchFor = methodInfo.Name;
                if (IsMethodAsync(methodInfo))
                {
                    nameToSearchFor = nameToSearchFor.Substring(0, nameToSearchFor.Length - "Async".Length);
                }
                
                var res = serviceType.GetMethod(nameToSearchFor, methodInfo.GetParameters().Select(p => p.ParameterType).ToArray());
                if (res != null)
                {
                    // TODO check return type matches, for now it will fail when the call returns.
                    continue;
                }
                throw new Exception($"Could not method matching {methodInfo} on {typeof(TService)}");
            }   
        }
    }
}