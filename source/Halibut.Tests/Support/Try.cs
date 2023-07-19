using System;

namespace Halibut.Tests.Support
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