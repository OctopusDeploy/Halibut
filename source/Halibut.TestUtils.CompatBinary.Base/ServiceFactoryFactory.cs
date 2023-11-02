using System;
using Halibut.TestUtils.Contracts;
using Halibut.TestUtils.Contracts.Tentacle.Services;
using Halibut.TestUtils.SampleProgram.Base.Services;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.Capabilities;
using Octopus.Tentacle.Contracts.ScriptServiceV2;
using FileTransferService = Halibut.TestUtils.SampleProgram.Base.Services.FileTransferService;

namespace Halibut.TestUtils.SampleProgram.Base
{
    public class ServiceFactoryFactory
    {
        /// <summary>
        /// Used when this external binary has the service.
        /// ie Old/Previous Service.
        /// </summary>
        /// <returns></returns>
        public static CompatBaseDelegateServiceFactory CreateServiceFactory()
        {
            var services = new CompatBaseDelegateServiceFactory();
            if (SettingsHelper.IsWithStandardServices())
            {
                services.Register<IEchoService>(() => new EchoService())
                    .Register<IMultipleParametersTestService>(() => new MultipleParametersTestService())
                    .Register<IComplexObjectService>(() => new ComplexObjectService())
                    .Register<ILockService>(() => new LockService())
                    .Register<IReadDataStreamService>(() => new ReadDataStreamService());

                var singleCountingService = new CountingService();
                services.Register<ICountingService>(() => singleCountingService);
            }

            if (SettingsHelper.IsWithCachingService())
            {
                services.Register<ICachingService>(() => new CachingService());
            }

            if (SettingsHelper.IsWithTentacleServices())
            {
                services.Register<IFileTransferService>(() => new FileTransferService())
                    .Register<IScriptService>(() => new ScriptService())
                    .Register<IScriptServiceV2>(() => new ScriptServiceV2())
                    .Register<ICapabilitiesServiceV2>(() => new CapabilitiesServiceV2());
            }

            return services;
        }

        /// <summary>
        /// Used when the test CLR has the services
        /// ie Old/Previous Client calling latest services.
        /// </summary>
        /// <param name="clientWhichTalksToLatestHalibut"></param>
        /// <param name="realServiceEndpoint"></param>
        /// <returns></returns>
        public static CompatBaseDelegateServiceFactory CreateProxyingServicesServiceFactory(HalibutRuntime clientWhichTalksToLatestHalibut, ServiceEndPoint realServiceEndpoint)
        {
            var services = new CompatBaseDelegateServiceFactory();
            // No need to check if is with standard services since, the Test itself has the service and so controls what is available
            // or not. This will just pass on the request to the service that may or may not exist.

            RegisterDelegateService<IEchoService>(s => new DelegateEchoService(s));
            RegisterDelegateService<IMultipleParametersTestService>(s => new DelegateMultipleParametersTestService(s));
            RegisterDelegateService<IComplexObjectService>(s => new DelegateComplexObjectService(s));
            RegisterDelegateService<IFileTransferService>(s => new DelegateFileTransferService(s));
			RegisterDelegateService<IReadDataStreamService>(s => new DelegateReadDataStreamService(s));
            RegisterDelegateService<IScriptService>(s => new DelegateScriptService(s));
            RegisterDelegateService<IScriptServiceV2>(s => new DelegateScriptServiceV2(s));
            RegisterDelegateService<ICapabilitiesServiceV2>(s => new DelegateCapabilitiesServiceV2(s));
            RegisterDelegateService<ILockService>(s => new DelegateLockService(s));

            void RegisterDelegateService<T>(Func<T, T> createDelegateFunc)
            {
                var forwardingService = clientWhichTalksToLatestHalibut.CreateClient<T>(realServiceEndpoint);
                services.Register(() => createDelegateFunc(forwardingService));
            }

            // The ICachingService is not supported since, the new attributes are not available in the versions of Halibut in the compat library.
            return services;
        }
    }
}