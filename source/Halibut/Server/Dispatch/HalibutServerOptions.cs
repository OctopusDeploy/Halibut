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
using System.Threading.Tasks;
using Halibut.Protocol;
using Halibut.Server.Security;
using Halibut.Server.ServiceModel;
using Newtonsoft.Json;

namespace Halibut.Server.Dispatch
{
    public class HalibutServerOptions : IHalibutServerOptions
    {
        public HalibutServerOptions()
        {
            ServiceInvoker = new ServiceInvoker();
            Scheduler = TaskScheduler.Default;
            ServiceFactory = new ActivatorServiceFactory();
            RequestProcessorFactory = new RequestProcessorFactory();
            ClientCertificateValidator = certificate => CertificateValidationResult.Valid;

            Serializer = DefaultJsonSerializer.Factory();
        }

        #region IHalibutServerOptions Members

        public JsonSerializer Serializer { get; set; }
        public IServiceFactory ServiceFactory { get; set; }
        public IRequestProcessorFactory RequestProcessorFactory { get; set; }
        public IServiceInvoker ServiceInvoker { get; set; }
        public CertificateValidationCallback ClientCertificateValidator { get; set; }
        public TaskScheduler Scheduler { get; set; }

        #endregion
    }
}