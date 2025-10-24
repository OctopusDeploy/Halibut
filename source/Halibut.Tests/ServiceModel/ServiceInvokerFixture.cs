﻿using System;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.ServiceModel;
using Halibut.Tests.Builders;
using Halibut.Tests.Support;
using Halibut.Tests.TestServices;
using Halibut.Tests.Util;
using Halibut.TestUtils.Contracts;
using Halibut.Transport.Protocol;
using NUnit.Framework;

namespace Halibut.Tests.ServiceModel
{
        
    public class ServiceInvokerFixture : BaseTest
    {
        [Test]
        public async Task AsyncInvokeWithParamsOnAsyncService()
        {
            var serviceFactory = new ServiceFactoryBuilder()
                .WithService<IEchoService, IAsyncEchoService>(() => new AsyncEchoService())
                .Build();

            var value = Some.RandomAsciiStringOfLength(8);
            var sut = new ServiceInvoker(serviceFactory);
            var request = new RequestMessage
            {
                ServiceName = nameof(IEchoService),
                MethodName = nameof(IEchoService.SayHello),
                Params = new[] { value }
            };

            var response = await sut.InvokeAsync(request);
            response.Result.Should().Be($"{value}...");
        }

        [Test]
        public async Task AsyncInvokeWithNoParamsOnAsyncService()
        {
            var serviceFactory = new ServiceFactoryBuilder()
                .WithService<ICountingService, IAsyncCountingService>(() => new AsyncCountingService())
                .Build();

            var sut = new ServiceInvoker(serviceFactory);
            var request = new RequestMessage
            {
                ServiceName = nameof(ICountingService),
                MethodName = nameof(ICountingService.Increment)
            };

            var response = await sut.InvokeAsync(request);
            response.Result.Should().Be(1);
        }

        [Test]
        public async Task AsyncInvokeWithNullableParamsOnAsyncService()
        {
            var serviceFactory = new ServiceFactoryBuilder()
                .WithConventionVerificationDisabled()
                .WithService<ICountingService, IAsyncCountingService>(() => new AsyncCountingService())
                .Build();

            var sut = new ServiceInvoker(serviceFactory);
            var request = new RequestMessage()
            {
                ServiceName = nameof(ICountingService),
                MethodName = nameof(ICountingService.Increment),
                Params = new object[] { null! },
            };

            var response = await sut.InvokeAsync(request);
            response.Result.Should().Be(1);
        }

        [Test]
        public async Task AsyncInvokeWithNoParams_AsyncServiceMissingSuffix()
        {
            var serviceFactory = new ServiceFactoryBuilder()
                .WithConventionVerificationDisabled()
                .WithService<IBrokenConventionService, IAsyncBrokenConventionService>(() => new AsyncBrokenConventionService())
                .Build();

            var sut = new ServiceInvoker(serviceFactory);
            var request = new RequestMessage()
            {
                ServiceName = nameof(IBrokenConventionService),
                MethodName = nameof(IBrokenConventionService.GetRandomNumberMissingSuffix)
            };

            await AssertException.Throws<Exception>(() => sut.InvokeAsync(request));
        }

        [Test]
        public async Task AsyncInvokeWithNoParams_AsyncServiceMissingCancellationToken()
        {
            var serviceFactory = new ServiceFactoryBuilder()
                .WithConventionVerificationDisabled()
                .WithService<IBrokenConventionService, IAsyncBrokenConventionService>(() => new AsyncBrokenConventionService())
                .Build();

            var sut = new ServiceInvoker(serviceFactory);
            var request = new RequestMessage()
            {
                ServiceName = nameof(IBrokenConventionService),
                MethodName = nameof(IBrokenConventionService.GetRandomNumberMissingCancellationToken)
            };

            await AssertException.Throws<Exception>(() => sut.InvokeAsync(request));
        }

        [Test]
        public async Task AsyncInvokeWithParams_AsyncServiceMissingSuffix()
        {
            var serviceFactory = new ServiceFactoryBuilder()
                .WithConventionVerificationDisabled()
                .WithService<IBrokenConventionService, IAsyncBrokenConventionService>(() => new AsyncBrokenConventionService())
                .Build();

            var sut = new ServiceInvoker(serviceFactory);
            var value = Some.RandomAsciiStringOfLength(8);
            var request = new RequestMessage()
            {
                ServiceName = nameof(IBrokenConventionService),
                MethodName = nameof(IBrokenConventionService.SayHelloMissingSuffix),
                Params = new[] { value }
            };

            await AssertException.Throws<Exception>(() => sut.InvokeAsync(request));
        }

        [Test]
        public async Task AsyncInvokeWithParams_AsyncServiceMissingCancellationToken()
        {
            var serviceFactory = new ServiceFactoryBuilder()
                .WithConventionVerificationDisabled()
                .WithService<IBrokenConventionService, IAsyncBrokenConventionService>(() => new AsyncBrokenConventionService())
                .Build();

            var sut = new ServiceInvoker(serviceFactory);
            var request = new RequestMessage()
            {
                ServiceName = nameof(IBrokenConventionService),
                MethodName = nameof(IBrokenConventionService.SayHelloMissingCancellationToken)
            };

            await AssertException.Throws<Exception>(() => sut.InvokeAsync(request));
        }
    }
}
