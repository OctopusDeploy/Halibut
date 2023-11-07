using System;
using System.Collections.Generic;
using Halibut.ServiceModel;
using Halibut.Tests.Util;

namespace Halibut.Tests.Builders
{
    public class ServiceFactoryBuilder
    {
        bool _conventionVerificationDisabled;
        DelegateServiceFactory factoryWithConventionVerification = new();
        NoSanityCheckingDelegateServiceFactory factoryWithNoConventionVerification = new();

        List<Exception> conventionExceptions = new List<Exception>();

        public ServiceFactoryBuilder WithService<TContract, TClientContract>(Func<TClientContract> factoryFunc)
        {
            try
            {
                factoryWithConventionVerification.Register<TContract, TClientContract>(factoryFunc);
            }
            // Convention verification may throw, but that just means we're probably going to use
            // the other factory anyway, so we don't care!
            catch (Exception e)
            {
                conventionExceptions.Add(e);
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
                if (conventionExceptions.Count > 0) throw new AggregateException(conventionExceptions);
                return factoryWithConventionVerification;
            }
        }
    }
}
