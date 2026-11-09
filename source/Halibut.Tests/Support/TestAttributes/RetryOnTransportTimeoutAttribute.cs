using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;
using NUnit.Framework.Internal.Commands;

namespace Halibut.Tests.Support.TestAttributes
{
    /// <summary>
    /// Retries a test only when it fails with a transport read timeout (see <see cref="TransportTimeoutClassifier"/>).
    /// Any other failure (assertion, protocol error, unexpected exception) fails immediately, as before.
    ///
    /// By default this only applies on .NET Framework, where LEV-2253 shows intermittent read timeouts
    /// blocking the nightly build chain. Every retry is reported (test output, and a TeamCity build tag and
    /// warning when running under TeamCity) so the underlying problem stays visible while it is investigated.
    ///
    /// NUnit's built-in [Retry] cannot be used: it only retries assertion failures, not exceptions.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public class RetryOnTransportTimeoutAttribute : NUnitAttribute, IRepeatTest, IWrapTestMethod
    {
        public const string TeamCityBuildTag = "halibut-transport-timeout-retry";

        static readonly ConditionalWeakTable<Test, ExceptionHolder> LastExceptionByTest = new();

        readonly int tryCount;
        readonly bool netFrameworkOnly;

        /// <param name="tryCount">Total number of attempts, including the first.</param>
        /// <param name="netFrameworkOnly">When true, tests run once (no retries) on modern .NET.</param>
        public RetryOnTransportTimeoutAttribute(int tryCount = 3, bool netFrameworkOnly = true)
        {
            if (tryCount < 1) throw new ArgumentOutOfRangeException(nameof(tryCount), "Must be at least 1");
            this.tryCount = tryCount;
            this.netFrameworkOnly = netFrameworkOnly;
        }

        static bool IsNetFramework =>
#if NETFRAMEWORK
            true;
#else
            false;
#endif

        public TestCommand Wrap(TestCommand command)
        {
            // Called for both IWrapTestMethod (innermost, around the test method) and IRepeatTest (outermost, around SetUp/TearDown).
            return command is TestMethodCommand
                ? new CaptureExceptionCommand(command)
                : new RetryCommand(command, netFrameworkOnly && !IsNetFramework ? 1 : tryCount);
        }

        class ExceptionHolder
        {
            public Exception? Exception;
        }

        static ExceptionHolder HolderFor(Test test) => LastExceptionByTest.GetValue(test, _ => new ExceptionHolder());

        class CaptureExceptionCommand : DelegatingTestCommand
        {
            public CaptureExceptionCommand(TestCommand innerCommand) : base(innerCommand)
            {
            }

            public override TestResult Execute(TestExecutionContext context)
            {
                try
                {
                    return innerCommand.Execute(context);
                }
                catch (Exception ex)
                {
                    HolderFor(context.CurrentTest).Exception = ex;
                    throw;
                }
            }
        }

        class RetryCommand : DelegatingTestCommand
        {
            readonly int tryCount;

            public RetryCommand(TestCommand innerCommand, int tryCount) : base(innerCommand)
            {
                this.tryCount = tryCount;
            }

            public override TestResult Execute(TestExecutionContext context)
            {
                var holder = HolderFor(context.CurrentTest);

                for (var attempt = 1; ; attempt++)
                {
                    holder.Exception = null;

                    try
                    {
                        context.CurrentResult = innerCommand.Execute(context);
                    }
                    catch (Exception ex)
                    {
                        context.CurrentResult ??= context.CurrentTest.MakeTestResult();
                        context.CurrentResult.RecordException(ex);
                        holder.Exception ??= ex;
                    }

                    var failed = context.CurrentResult.ResultState.Status == TestStatus.Failed;
                    if (!failed || attempt >= tryCount || !TransportTimeoutClassifier.IsTransportTimeout(holder.Exception))
                    {
                        return context.CurrentResult;
                    }

                    ReportRetry(context, attempt, holder.Exception!);

                    context.CurrentResult = context.CurrentTest.MakeTestResult();
                    context.CurrentRepeatCount++;
                }
            }

            void ReportRetry(TestExecutionContext context, int attempt, Exception exception)
            {
                var message = $"LEV-2253: {context.CurrentTest.FullName} failed with a transport timeout on attempt {attempt} of {tryCount}; retrying. {exception.GetBaseException().GetType().Name}: {exception.GetBaseException().Message}";

                TestContext.Progress.WriteLine(message);

                if (TeamCityDetection.IsRunningInTeamCity())
                {
                    TestContext.Progress.WriteLine($"##teamcity[addBuildTag '{TeamCityBuildTag}']");
                    TestContext.Progress.WriteLine($"##teamcity[message text='{EscapeForTeamCity(message)}' status='WARNING']");
                }
            }

            static string EscapeForTeamCity(string value) =>
                value.Replace("|", "||")
                    .Replace("'", "|'")
                    .Replace("\n", "|n")
                    .Replace("\r", "|r")
                    .Replace("[", "|[")
                    .Replace("]", "|]");
        }
    }
}
