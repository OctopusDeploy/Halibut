using System;
using System.Collections.Generic;
using System.Linq;
using Halibut.Exceptions;
using Halibut.Util;

namespace Halibut.ServiceModel
{
    public class DelegateServiceFactory : IServiceFactory
    {
        readonly Dictionary<string, Func<object>> services = new Dictionary<string, Func<object>>(StringComparer.OrdinalIgnoreCase);
        readonly HashSet<Type> serviceTypes = new HashSet<Type>();

        public DelegateServiceFactory Register<TContract, TAsyncContract>(Func<TAsyncContract> implementation)
        {
            AsyncServiceVerifier.VerifyAsyncSurfaceAreaFollowsConventions<TContract, TAsyncContract>();
            
            var serviceType = typeof(TContract);
            services.Add(serviceType.Name, () => implementation()!);
            lock (serviceTypes)
            {
                serviceTypes.Add(serviceType);
            }

            return this;
        }

        public IServiceLease CreateService(string serviceName)
        {
            var serviceType = GetService(serviceName);
            return CreateService(serviceType);
        }

        Func<object> GetService(string name)
        {
            if (!services.TryGetValue(name, out var result))
            {
                throw new ServiceNotFoundHalibutClientException("Service not found: " + name);
            }

            return result;
        }

        static IServiceLease CreateService(Func<object> serviceBuilder)
        {
            var service = serviceBuilder();
            return new Lease(service);
        }
        
        public IReadOnlyList<Type> RegisteredServiceTypes
        {
            get
            {
                lock (serviceTypes)
                {
                    return serviceTypes.ToList();    
                }
            }
        }

        #region Nested type: Lease

        class Lease : IServiceLease
        {
            readonly object service;

            public Lease(object service)
            {
                this.service = service;
            }

            public object Service => service;

            public void Dispose()
            {
                if (service is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
        }

        #endregion
    }
}
