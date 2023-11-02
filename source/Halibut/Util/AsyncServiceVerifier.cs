using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Exceptions;

namespace Halibut.Util
{
    public class AsyncServiceVerifier
    {
        /// <summary>
        /// This method enforces that all methods on the sync service have a corresponding method on the async service which adheres to the following conventions:
        /// (1) Name must contain 'Async' suffix
        /// (2) Return Type must be Task (if sync method return type is 'void') or Task&lt;T&gt; (where T is sync method return type)
        /// (3) Last parameter must be of type CancellationToken
        /// </summary>
        /// <typeparam name="TSyncService">The type of the sync service</typeparam>
        /// <typeparam name="TAsyncService">The type of the async service</typeparam>
        /// <exception cref="NoMatchingServiceOrMethodHalibutClientException">Thrown when the async service does not map to the sync service according to the expected conventions.</exception>
        public static void VerifyAsyncSurfaceAreaFollowsConventions<TSyncService, TAsyncService>()
        {
            const string Async = "Async";
            
            Type syncService = typeof(TSyncService);
            Type asyncService = typeof(TAsyncService);
            
            var syncServiceMethods = syncService.GetMethods();

            bool ReturnTypesMatch(MethodInfo syncMethod, MethodInfo asyncMethod)
            {
                if (syncMethod.ReturnType == typeof(void))
                {
                    return typeof(Task).IsAssignableFrom(asyncMethod.ReturnType);
                }

                return typeof(Task<>).MakeGenericType(syncMethod.ReturnType)
                    .IsAssignableFrom(asyncMethod.ReturnType);
            }

            foreach (var syncMethod in syncServiceMethods)
            {
                var asyncMethod = asyncService.GetMethod(
                    syncMethod.Name + Async,
                    syncMethod.GetParameters().Select(p => p.ParameterType).Append(typeof(CancellationToken)).ToArray());

                if (asyncMethod == null)
                {
                    throw new NoMatchingServiceOrMethodHalibutClientException(
                        $"The service '{asyncService.Name} does not follow the conventions for providing an async implementation of '{syncService.Name}. Could not find matching method for: " +
                        $"{syncMethod} searching by name and parameters (with a cancellation token at the end).");
                }

                if (!ReturnTypesMatch(syncMethod, asyncMethod))
                {
                    throw new NoMatchingServiceOrMethodHalibutClientException(
                        $"The service '{asyncService.Name} does not follow the conventions for providing an async implementation of '{syncService.Name}. Find matching method for: " +
                        $"{syncMethod} but the return types did not match");
                }
            }
        }
    }
}
