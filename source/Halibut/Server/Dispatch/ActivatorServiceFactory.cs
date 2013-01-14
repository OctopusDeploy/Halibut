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
using Halibut.Server.ServiceModel;

namespace Halibut.Server.Dispatch
{
    public class ActivatorServiceFactory : IServiceFactory
    {
        public IServiceLease CreateService(Type serviceType)
        {
            var service = Activator.CreateInstance(serviceType);
            return new Lease(service);
        }

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
                    ((IDisposable) service).Dispose();
                }
            }
        }
    }
}