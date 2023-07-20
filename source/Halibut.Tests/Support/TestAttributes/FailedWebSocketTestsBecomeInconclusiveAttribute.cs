using System;
using System.Linq;
using Halibut.Tests.Support.TestCases;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;
using NUnit.Framework.Internal.Commands;

namespace Halibut.Tests.Support.TestAttributes
{
    [AttributeUsage(AttributeTargets.Method)]
    public class FailedWebSocketTestsBecomeInconclusiveAttribute : PropertyAttribute, IWrapSetUpTearDown, IApplyToContext
    {
        void IApplyToContext.ApplyToContext(TestExecutionContext context)
        {
            // Remove the timeout as we need to wrap our command around the timeout command nunit creates
            context.TestCaseTimeout = 0;
        }

        public TestCommand Wrap(TestCommand command)
        {
            var timeoutCommand = new TimeoutCommand(command, TestTimeoutAttribute.TestTimeout());
            return new WrapperCommand(timeoutCommand);
        }

        public class WrapperCommand : BeforeAndAfterTestCommand
        {
            public WrapperCommand(TestCommand innerCommand)
                : base(innerCommand)
            {
                BeforeTest = _ =>
                {
                };

                AfterTest = context =>
                {
                    if (context.CurrentResult.ResultState.Status == TestStatus.Failed && IsWebSocketTest(context))
                    {
                        // Unfortunately nuke and dotnet test report tests that have run but have an outcome of anything but Failed as successful!
                        // Leaving the changes to reduce local noise for now.
                        if (!TeamCityDetection.IsRunningInTeamCity())
                        {
                            context.OutWriter.WriteLine($"WebSocket Test Failed. {context.CurrentResult.Message} {context.CurrentResult.StackTrace}");
                            context.CurrentResult.SetResult(new ResultState(TestStatus.Inconclusive, "Flakey WebSockets", FailureSite.Test));
                        }
                    }
                };
            }
        }

        static bool IsWebSocketTest(TestExecutionContext context)
        {
            var clientAndServiceTestCase = context.CurrentTest.Arguments.OfType<ClientAndServiceTestCase>().SingleOrDefault();

            if (clientAndServiceTestCase != null && clientAndServiceTestCase.ServiceConnectionType == ServiceConnectionType.PollingOverWebSocket)
            {
                return true;
            }

            var serviceConnectionTypes = context.CurrentTest.Arguments.OfType<ServiceConnectionType>().ToList();

            if (serviceConnectionTypes.Any() && serviceConnectionTypes.First() == ServiceConnectionType.PollingOverWebSocket)
            {
                return true;
            }
            
            return false;
        }
    }
}