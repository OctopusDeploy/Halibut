using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Halibut.Util
{
    public class AwaitAllAndIgnoreException : IAsyncDisposable
    {
        List<Task> tasks = new List<Task>();

        public void AddTasks(params Task[] tasksToAdd)
        {
            foreach (var task in tasksToAdd)
            {
#pragma warning disable VSTHRD003
                tasks.Add(Try.IgnoringError(async () => await task));
#pragma warning restore VSTHRD003
            }
        }
        
        public async ValueTask DisposeAsync()
        {
            await Task.WhenAll(tasks);
        }
    }
}