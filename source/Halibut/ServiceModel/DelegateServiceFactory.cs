using System;
using System.Collections.Generic;
using Janitor;

namespace Halibut.ServiceModel
{
    public class DelegateServiceFactory : IServiceFactory
    {
        readonly Dictionary<string, Func<object>> services = new Dictionary<string, Func<object>>(StringComparer.OrdinalIgnoreCase);

        public void Register<TContract>(Func<TContract> implementation)
        {
            services.Add(typeof(TContract).Name, () => implementation());
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

        #region Nested type: Lease

        [SkipWeaving]
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