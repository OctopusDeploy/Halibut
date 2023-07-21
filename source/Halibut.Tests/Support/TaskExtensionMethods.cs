using System.Threading.Tasks;

namespace Halibut.Tests.Support
{
    public static class TaskExtensionMethods
    {
        public static async Task AwaitIfFaulted(this Task task)
        {
            if (task.IsFaulted) await task;
        }
    }
}