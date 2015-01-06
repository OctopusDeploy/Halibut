using System;
using System.Threading;

namespace Halibut.Services
{
    public class ActiveRemoteServiceAgent : IRemoteServiceAgent
    {
        readonly SecureClient client;
        DateTimeOffset nextPoll = DateTimeOffset.UtcNow;
        int working;

        public ActiveRemoteServiceAgent(SecureClient client)
        {
            this.client = client;
        }

        public bool ProcessNext()
        {
            var hasWork = !client.IsEmpty || DateTimeOffset.UtcNow > nextPoll;
            if (!hasWork) return false;

            var value = Interlocked.CompareExchange(ref working, 1, 0);
            if (value == 0)
            {
                ThreadPool.QueueUserWorkItem(AgentThreadExecutor);
                return true;
            }

            return false;
        }

        private void AgentThreadExecutor(object ignored)
        {
            try
            {
                Console.WriteLine("Perform exchange");

                var exchanged = client.PerformExchange();

                while (!client.IsEmpty)
                {
                    exchanged += client.PerformExchange();
                }

                if (exchanged > 0)
                {
                    nextPoll = DateTimeOffset.UtcNow;
                }
                else
                {
                    nextPoll = DateTimeOffset.UtcNow.AddSeconds(10);
                }
            }
            finally
            {
                working = 0;
            }
        }

        public void Dispose()
        {
            client.Dispose();
        }
    }
}