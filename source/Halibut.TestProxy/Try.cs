using System;
using System.Threading.Tasks;

namespace Halibut.TestProxy
{
    public static class Try
    {
        public static void CatchingError(Action tryThisAction, Action<Exception> onFailure)
        {
            try
            {
                tryThisAction();
            }
            catch (Exception e)
            {
                onFailure(e);
            }
        }

        public static async Task<Exception?> CatchingError(Func<Task> tryThisAction)
        {
            try
            {
                await tryThisAction();
            }
            catch (Exception e)
            {
                return e;
            }

            return null;
        }
    }
}