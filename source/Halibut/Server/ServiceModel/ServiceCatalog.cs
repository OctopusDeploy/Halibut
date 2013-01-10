// Copyright 2012-2013 Octopus Deploy Pty. Ltd.
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//    http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;

namespace Halibut.Server.ServiceModel
{
    public class ServiceCatalog : IServiceCatalog
    {
        readonly Dictionary<string, Type> services = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);

        public void Register(Type contract, Type implementation)
        {
            services.Add(contract.Name, implementation);
        }

        public void Register<TContract, TImplementation>() where TImplementation : TContract
        {
            Register(typeof (TContract), typeof (TImplementation));
        }

        public Type GetService(string name)
        {
            Type result;
            if (!services.TryGetValue(name, out result))
            {
                throw new Exception("Service not found: " + name);
            }

            return result;
        }
    }
}