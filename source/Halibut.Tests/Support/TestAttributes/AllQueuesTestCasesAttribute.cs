using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Halibut.ServiceModel;
using Halibut.Tests.Builders;
using Halibut.Tests.Support.BackwardsCompatibility;
using Halibut.Tests.Support.TestCases;
using Halibut.Tests.TestSetup.Redis;
using NUnit.Framework;

namespace Halibut.Tests.Support.TestAttributes
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
    public class AllQueuesTestCasesAttribute : HalibutTestCaseSourceAttribute
    {
        public AllQueuesTestCasesAttribute() :
            base(
                typeof(PendingRequestQueueFactories),
                nameof(PendingRequestQueueFactories.GetEnumerator), null)
        {
        }
        
        static class PendingRequestQueueFactories
        {
            public static IEnumerable GetEnumerator()
            {
                var factories = new List<PendingRequestQueueTestCase>();
#if NET8_0_OR_GREATER
                if (EnsureRedisIsAvailableSetupFixture.WillRunRedisTests)
                {
                    factories.Add(new PendingRequestQueueTestCase("Redis", () => new RedisPendingRequestQueueBuilder()));
                }
#endif
                factories.Add(new PendingRequestQueueTestCase("InMemory", () => new PendingRequestQueueBuilder()));

                return factories;
            }
        }
    }

    public class PendingRequestQueueTestCase
    {
        public readonly string Name;
        private Func<IPendingRequestQueueBuilder> BuilderBuilder { get; }
        
        public IPendingRequestQueueBuilder Builder => BuilderBuilder();

        public PendingRequestQueueTestCase(string name, Func<IPendingRequestQueueBuilder> builder)
        {
            Name = name;
            BuilderBuilder = builder;
        }
        
        public override string ToString() => Name;

        protected bool Equals(PendingRequestQueueTestCase other)
        {
            return Name == other.Name;
        }

        public override bool Equals(object? obj)
        {
            if (obj is null) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((PendingRequestQueueTestCase)obj);
        }

        public override int GetHashCode()
        {
            return Name.GetHashCode();
        }
    }
}
