using System;
using System.Reflection;
using Halibut.Diagnostics;
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq.Expressions;
using Halibut.Tests.Util;

namespace Halibut.Tests
{
    [SetUpFixture]
    public class LowerHalibutLimitsForAllTests
    {
        [OneTimeSetUp]
        public static void  LowerHalibutLimits()
        {

            var halibutLimitType = typeof(HalibutLimits);
            
            halibutLimitType.ReflectionSetFieldValue(nameof(HalibutLimits.PollingRequestQueueTimeout), TimeSpan.FromSeconds(20));
            halibutLimitType.ReflectionSetFieldValue(nameof(HalibutLimits.PollingRequestMaximumMessageProcessingTimeout), TimeSpan.FromSeconds(20));
            halibutLimitType.ReflectionSetFieldValue(nameof(HalibutLimits.RetryListeningSleepInterval), TimeSpan.FromSeconds(1));
            halibutLimitType.ReflectionSetFieldValue(nameof(HalibutLimits.ConnectionErrorRetryTimeout), TimeSpan.FromSeconds(5));
            
            // Intentionally set higher than the heart beat, since some tests need to determine that the hart beat timeout applies.
            halibutLimitType.ReflectionSetFieldValue(nameof(HalibutLimits.TcpClientSendTimeout), TimeSpan.FromSeconds(45));
            halibutLimitType.ReflectionSetFieldValue(nameof(HalibutLimits.TcpClientReceiveTimeout), TimeSpan.FromSeconds(45));
            
            halibutLimitType.ReflectionSetFieldValue(nameof(HalibutLimits.TcpClientHeartbeatSendTimeout), TimeSpan.FromSeconds(5));
            halibutLimitType.ReflectionSetFieldValue(nameof(HalibutLimits.TcpClientHeartbeatReceiveTimeout), TimeSpan.FromSeconds(5));
            halibutLimitType.ReflectionSetFieldValue(nameof(HalibutLimits.TcpClientConnectTimeout), TimeSpan.FromSeconds(5));
            halibutLimitType.ReflectionSetFieldValue(nameof(HalibutLimits.PollingQueueWaitTimeout), TimeSpan.FromSeconds(20));
        }
    }
}