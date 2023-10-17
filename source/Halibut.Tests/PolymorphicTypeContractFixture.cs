using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Tests.Support;
using Halibut.Tests.Support.TestAttributes;
using Halibut.Tests.Support.TestCases;
using Halibut.Tests.TestServices.Async;
using Halibut.Tests.Util;
using Halibut.TestUtils.Contracts;
using Halibut.Util;
using NUnit.Framework;

namespace Halibut.Tests
{
    public class PolymorphicTypeContractFixture : BaseTest
    {

        [Test]
        [LatestClientAndLatestServiceTestCases(testAsyncServicesAsWell: true, testSyncService:false, testNetworkConditions: false)]
        public async Task ExecuteServiceWithPolymorphicContract(ClientAndServiceTestCase clientAndServiceTestCase)
        {
            var clientAndService = await clientAndServiceTestCase.CreateTestCaseBuilder()
                .AsLatestClientAndLatestServiceBuilder()
                .WithAsyncService<IPolymorphicService, IAsyncPolymorphicService>(() => new AsyncPolymorphicService())
                .Build(CancellationToken);

            var polymorphicServiceClient = clientAndService.CreateClient<IPolymorphicService, IAsyncClientPolymorphicService>();

            (await polymorphicServiceClient.ExecuteScriptAsync(new ExecuteScriptCommand("NoOp", new LocalEnvironment())))
                .Should()
                .Be("Local:NoOp");

            (await polymorphicServiceClient.ExecuteScriptAsync(new ExecuteScriptCommand("NoOp", new KubernetesJobEnvironment("Image", "https://dockerhub.com"))))
                .Should()
                .Be("K8s:NoOp:Imagehttps://dockerhub.com");
        }
    }
    
    public interface IPolymorphicService
    {
        string ExecuteScript(ExecuteScriptCommand command);
    }

    public interface IAsyncPolymorphicService
    {
        Task<string> ExecuteScriptAsync(ExecuteScriptCommand command, CancellationToken cancellationToken);
    }
    
    public interface IAsyncClientPolymorphicService
    {
        Task<string> ExecuteScriptAsync(ExecuteScriptCommand command);    
    }

    public class AsyncPolymorphicService : IAsyncPolymorphicService
    {
        public async Task<string> ExecuteScriptAsync(ExecuteScriptCommand command, CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
             return command.ExecutionEnvironment switch
             {
                 LocalEnvironment _ => $"Local:{command.Script}",
                 KubernetesJobEnvironment k8s => $"K8s:{command.Script}:{k8s.ContainerImage}{k8s.ContainerImageRepositoryUrl}",
                 _ => throw new ArgumentOutOfRangeException()
             };
        }
    }

    public class ExecuteScriptCommand
    { 
        public ExecuteScriptCommand(string script, IExecutionEnvironment executionEnvironment)
        {
            Script = script;
            ExecutionEnvironment = executionEnvironment;
        }

        public string Script { get; }
        public IExecutionEnvironment ExecutionEnvironment { get; }
    }

    public interface IExecutionEnvironment
    {
    }

    public class LocalEnvironment : IExecutionEnvironment
    {
    }

    public class KubernetesJobEnvironment : IExecutionEnvironment
    {
        public KubernetesJobEnvironment(string containerImage, string containerImageRepositoryUrl)
        {
            ContainerImage = containerImage;
            ContainerImageRepositoryUrl = containerImageRepositoryUrl;
        }

        public string ContainerImage { get; }
        public string ContainerImageRepositoryUrl { get;  }
    }
}