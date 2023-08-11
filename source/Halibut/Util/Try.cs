using System;

namespace Halibut.Util
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
    }
    
}