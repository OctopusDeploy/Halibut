using System.Threading.Tasks;
using Halibut.TestUtils.SampleProgram.Base;

namespace Halibut.TestUtils.SampleProgram.v6_0_658
{
    public class Program
    {
        public static async Task<int> Main()
        {
            return await BackwardsCompatProgramBase.Main();
        }
    }
}