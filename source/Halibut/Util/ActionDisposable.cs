using System;

namespace Halibut.Util
{
    public class ActionDisposable : IDisposable
    {
        Action action;

        public ActionDisposable(Action action)
        {
            this.action = action;
        }

        public void Dispose()
        {
            if (action != null)
            {
                var localAction = action;
                this.action = null;
                localAction();
            }
        }
    }
}