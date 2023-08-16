using System;
using Halibut.ServiceModel;
using Halibut.Tests.Util;

namespace Halibut.Tests.Builders
{
    public class ServiceFactoryBuilder
    {
        bool _conventionVerificationDisabled;
        DelegateServiceFactory factoryWithConventionVerification = new();
        NoSanityCheckingDelegateServiceFactory factoryWithNoConventionVerification = new();
        

        public ServiceFactoryBuilder WithService<TContract>(Func<TContract> factoryFunc)
        {
            factoryWithConventionVerification.Register(factoryFunc);
            factoryWithNoConventionVerification.Register(factoryFunc);
            return this;
        }
        

        public ServiceFactoryBuilder WithService<TContract, TClientContract>(Func<TClientContract> factoryFunc)
        {
            try
            {
                factoryWithConventionVerification.Register<TContract, TClientContract>(factoryFunc);
            }
            // Convention verification may throw, but that just means we're probably going to use
            // the other factory anyway, so we don't care!
            catch
            {
            }

            factoryWithNoConventionVerification.Register<TContract, TClientContract>(factoryFunc);
            return this;
        }

        public ServiceFactoryBuilder WithConventionVerificationDisabled()
        {
            _conventionVerificationDisabled = true;
            return this;
        }

        public IServiceFactory Build()
        {
            if (_conventionVerificationDisabled)
            {
                return factoryWithNoConventionVerification;
            }
            else
            {
                return factoryWithConventionVerification;
            }
        }
    }
}
