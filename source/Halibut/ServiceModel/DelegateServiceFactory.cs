// Copyright 2012-2013 Octopus Deploy Pty. Ltd.
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//   http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;

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