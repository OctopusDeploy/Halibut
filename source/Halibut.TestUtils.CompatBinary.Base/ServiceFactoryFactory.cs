using Halibut.ServiceModel;
using Halibut.TestUtils.Contracts;

namespace Halibut.TestUtils.SampleProgram.Base
{
    public class ServiceFactoryFactory
    {
        /// <summary>
        /// Used when this external binary has the service.
        /// ie Old/Previous Service.
        /// </summary>
        /// <returns></returns>
        public static DelegateServiceFactory CreateServiceFactory()
        {
            var services = new DelegateServiceFactory();
            services.Register<IEchoService>(() => new EchoService());
            services.Register<ICachingService>(() => new CachingService());
            services.Register<IMultipleParametersTestService>(() => new MultipleParametersTestService());
            return services;
        }

        /// <summary>
        /// Used when the test CLR has the services
        /// ie Old/Previous Client calling latest services.
        /// </summary>
        /// <param name="clientWhichTalksToLatestHalibut"></param>
        /// <param name="realServiceEndpoint"></param>
        /// <returns></returns>
        public static DelegateServiceFactory CreateProxyingServicesServiceFactory(HalibutRuntime clientWhichTalksToLatestHalibut, ServiceEndPoint realServiceEndpoint)
        {
            var services = new DelegateServiceFactory();
            
            var forwardingEchoService = clientWhichTalksToLatestHalibut.CreateClient<IEchoService>(realServiceEndpoint);
            services.Register<IEchoService>(() => new DelegateEchoService(forwardingEchoService));
            
            var forwardingMultipleParametersTestService = clientWhichTalksToLatestHalibut.CreateClient<IMultipleParametersTestService>(realServiceEndpoint);
            services.Register<IMultipleParametersTestService>(() => new DelegateMultipleParametersTestService(forwardingMultipleParametersTestService));
            
            // The ICachingService is not supported since, the new attributes are not available in the versions of Halibut in the compat library.
            return services;
        }
    }
}
