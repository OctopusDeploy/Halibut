using System;
using System.Text;
using FluentAssertions;
using Halibut.Tests.Support.TestAttributes;
using NUnit.Framework;

namespace Halibut.Tests
{
    public class HowManyCpus : BaseTest
    {
        [Test]
        public void HowMany()
        {
            var count = Environment.ProcessorCount;
            Logger.Error("The count is: " + count);
            count.Should().Be(0);
        }

        [Test]
        public void HowManyParallelTests()
        {
            int count = CustomLevelOfParallelismAttribute.NumberOfCpusToUse();
            
            Logger.Error("The test count is: " + count);
            count.Should().Be(0);
        }
        
        [Test]
        public void EnvVars()
        {
            var env = Environment.GetEnvironmentVariables();

            var sb = new StringBuilder();
            foreach (var entry in env.Keys)
            {
                sb.Append(entry.ToString())
                    .Append("=")
                    .Append(env[entry])
                    .Append("\r\n");
            }
            
            Logger.Error("Env keys: " + sb.ToString());
            sb.ToString().Should().Be("");
        }
    }
}