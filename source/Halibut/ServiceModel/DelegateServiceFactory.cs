using System;
using System.Collections.Generic;
using System.Linq;

namespace Halibut.ServiceModel
{
    public class DelegateServiceFactory : IServiceFactory
    {
        readonly Dictionary<string, Func<object>> services = new Dictionary<string, Func<object>>(StringComparer.OrdinalIgnoreCase);
        readonly HashSet<Type> serviceTypes = new HashSet<Type>();

        public void Register<TContract>(Func<TContract> implementation)
        {
            var serviceType = typeof(TContract);
            services.Add(serviceType.Name, () => implementation());
            serviceTypes.Add(serviceType);
        }

        public IServiceLease CreateService(string serviceName)
        {
            var serviceType = GetService(serviceName);
            return CreateService(serviceType);
        }

        Func<object> GetService(string name)
        {
            Func<object> result;
            if (!services.TryGetValue(name, out result))
            {
                throw new Exception("Service not found: " + name);
            }

            return result;
        }

        static IServiceLease CreateService(Func<object> serviceBuilder)
        {
            var service = serviceBuilder();
            return new Lease(service);
        }
        
        public IReadOnlyList<Type> RegisteredServiceTypes => serviceTypes.ToList();

        #region Nested type: Lease

        class Lease : IServiceLease
        {
            readonly object service;

            public Lease(object service)
            {
                this.service = service;
            }

            public object Service
            {
                get { return service; }
            }

            public void Dispose()
            {
                if (service is IDisposable)
                {
                    ((IDisposable)service).Dispose();
                }
            }
        }

        #endregion
    }
}