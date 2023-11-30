using System.Threading.Tasks;

namespace Halibut.Tests.Support
{
    public static class TaskExtensionMethods
    {
        public static async Task AwaitIfFaulted(this Task task)
        {
#pragma warning disable VSTHRD003 // Avoid awaiting foreign Tasks
            if (task.IsFaulted) await task;
#pragma warning restore VSTHRD003 // Avoid awaiting foreign Tasks
        }
    }
}