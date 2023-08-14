using System;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Exceptions;
using Halibut.ServiceModel;
using Halibut.Tests.Support;
using Halibut.Tests.Util;
using Halibut.TestUtils.Contracts;
using Halibut.Transport.Protocol;
using NUnit.Framework;

namespace Halibut.Tests.ServiceModel
{
        
    public class ServiceInvokerFixture : BaseTest
    {
        [Test]
        public void InvokeWithParams()
        {
            var serviceFactory = new DelegateServiceFactory()
                .Register<IEchoService>(() => new EchoService());

            var value = Some.RandomAsciiStringOfLength(8);
            var sut = new ServiceInvoker(serviceFactory);
            var request = new RequestMessage
            {
                ServiceName = nameof(IEchoService),
                MethodName = nameof(IEchoService.SayHello),
                Params = new[] { value }
            };

            var response = sut.Invoke(request);
            response.Result.Should().Be($"{value}...");
        }
        
        [Test]
        public void InvokeWithNoParams()
        {
            var serviceFactory = new DelegateServiceFactory()
                .Register<ICountingService>(() => new CountingService());

            var sut = new ServiceInvoker(serviceFactory);
            var request = new RequestMessage
            {
                ServiceName = nameof(ICountingService),
                MethodName = nameof(ICountingService.Increment)
            };

            var response = sut.Invoke(request);
            response.Result.Should().Be(1);
        }
        
        [Test]
        public async Task AsyncInvokeWithParamsOnAsyncService()
        {
            var serviceFactory = new DelegateServiceFactory()
                .Register<IEchoService, IAsyncEchoService>(() => new AsyncEchoService());

            var value = Some.RandomAsciiStringOfLength(8);
            var sut = new ServiceInvoker(serviceFactory);
            var request = new RequestMessage
            {
                ServiceName = nameof(IEchoService),
                MethodName = nameof(IEchoService.SayHello),
                Params = new[] { value }
            };

            var response = await sut.InvokeAsync(request);
            response.Result.Should().Be($"{value}Async...");
        }

        [Test]
        public async Task AsyncInvokeWithNoParamsOnAsyncService()
        {
            var serviceFactory = new DelegateServiceFactory()
                .Register<ICountingService, IAsyncCountingService>(() => new AsyncCountingService());

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
        public async Task AsyncInvokeWithParamsOnSyncService()
        {
            var serviceFactory = new DelegateServiceFactory()
                .Register<IEchoService>(() => new EchoService());

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
        public async Task AsyncInvokeWithNoParamsOnSyncService()
        {
            var serviceFactory = new DelegateServiceFactory()
                .Register<ICountingService, IAsyncCountingService>(() => new AsyncCountingService());

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
        public async Task AsyncInvokeWithNoParams_AsyncServiceMissingSuffix()
        {
            var serviceFactory = new DelegateServiceFactory()
                .RegisterWithNoVerification<IBrokenConventionService, IAsyncBrokenConventionService>(() => new AsyncBrokenConventionService());

            var sut = new ServiceInvoker(serviceFactory);
            var request = new RequestMessage()
            {
                ServiceName = nameof(IBrokenConventionService),
                MethodName = nameof(IBrokenConventionService.GetRandomNumberMissingSuffix)
            };

            await AssertAsync.Throws<Exception>(() => sut.InvokeAsync(request));
        }

        [Test]
        public async Task AsyncInvokeWithNoParams_AsyncServiceMissingCancellationToken()
        {
            var serviceFactory = new DelegateServiceFactory()
                .RegisterWithNoVerification<IBrokenConventionService, IAsyncBrokenConventionService>(() => new AsyncBrokenConventionService());

            var sut = new ServiceInvoker(serviceFactory);
            var request = new RequestMessage()
            {
                ServiceName = nameof(IBrokenConventionService),
                MethodName = nameof(IBrokenConventionService.GetRandomNumberMissingCancellationToken)
            };

            await AssertAsync.Throws<Exception>(() => sut.InvokeAsync(request));
        }

        [Test]
        public async Task AsyncInvokeWithParams_AsyncServiceMissingSuffix()
        {
            var serviceFactory = new DelegateServiceFactory()
                .RegisterWithNoVerification<IBrokenConventionService, IAsyncBrokenConventionService>(() => new AsyncBrokenConventionService());

            var sut = new ServiceInvoker(serviceFactory);
            var value = Some.RandomAsciiStringOfLength(8);
            var request = new RequestMessage()
            {
                ServiceName = nameof(IBrokenConventionService),
                MethodName = nameof(IBrokenConventionService.SayHelloMissingSuffix),
                Params = new[] { value }
            };

            await AssertAsync.Throws<Exception>(() => sut.InvokeAsync(request));
        }

        [Test]
        public async Task AsyncInvokeWithParams_AsyncServiceMissingCancellationToken()
        {
            var serviceFactory = new DelegateServiceFactory()
                .RegisterWithNoVerification<IBrokenConventionService, IAsyncBrokenConventionService>(() => new AsyncBrokenConventionService());

            var sut = new ServiceInvoker(serviceFactory);
            var request = new RequestMessage()
            {
                ServiceName = nameof(IBrokenConventionService),
                MethodName = nameof(IBrokenConventionService.SayHelloMissingCancellationToken)
            };

            await AssertAsync.Throws<Exception>(() => sut.InvokeAsync(request));
        }
    }
}
