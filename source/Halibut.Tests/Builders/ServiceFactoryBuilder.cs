using System;
using System.Collections.Generic;
using Halibut.ServiceModel;
using Halibut.Tests.Util;

namespace Halibut.Tests.Builders
{
    public class ServiceFactoryBuilder
    {
        bool conventionVerificationDisabled;
        readonly DelegateServiceFactory factoryWithConventionVerification = new();
        readonly NoSanityCheckingDelegateServiceFactory factoryWithNoConventionVerification = new();

        readonly List<Exception> conventionExceptions = new();

        public ServiceFactoryBuilder WithService<TContract, TClientContract>(Func<TClientContract> factoryFunc)
        {
            try
            {
                factoryWithConventionVerification.Register<TContract, TClientContract>(factoryFunc);
            }
            catch (Exception e)
            {
                conventionExceptions.Add(e);
            }

            factoryWithNoConventionVerification.Register<TContract, TClientContract>(factoryFunc);
            return this;
        }

        public ServiceFactoryBuilder WithConventionVerificationDisabled()
        {
            conventionVerificationDisabled = true;
            return this;
        }

        public IServiceFactory Build()
        {
            if (conventionVerificationDisabled)
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
