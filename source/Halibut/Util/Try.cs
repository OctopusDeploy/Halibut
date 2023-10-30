using System;
using System.IO;

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

        public static SilentStreamDisposer CatchingErrorOnDisposal(Stream streamToDispose, Action<Exception> onFailure)
        {
            return new SilentStreamDisposer(streamToDispose, onFailure);
        }
    }
    
}