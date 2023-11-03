using System;
using System.Threading;

namespace Halibut.TestUtils.SampleProgram.Base.Services
{
    public class FixHungWebSocketsHack
    {
        /// <summary>
        /// For unknown reasons a external net48 process in websockets appears to get itself into a
        /// state in which a polling websocket service wont read from a web socket. This is despite the
        /// client being blocked unable to send more data to that websocket.
        /// Looking at the CLR of the compat bin we can see that the task attempting to read from
        /// the socket is not yet activated, and never gets activated. Yet the CLR will execute other
        /// tasks. The Test CLR is also not stuck and continues to run other tasks.
        /// Amazingly writing to console, when it is in that stuck state results in the compat bin CLR deciding
        /// it ought to run that task to read from the websocket. This results in the test completing
        /// successfully.
        /// So here we are with this atrocity
        /// </summary>
        public static void EnableHack()
        {
            var t = new Thread(() =>
            {
                while (true)
                {
                    Thread.Sleep(10000);
                    // Print anything to console.
                    Console.WriteLine("Compat binary is still running");
                }
            });
            t.IsBackground = true;
            t.Start();
        }
    }
}