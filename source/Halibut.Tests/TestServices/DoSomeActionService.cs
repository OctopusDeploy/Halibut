using System;

namespace Halibut.Tests.TestServices
{
    public class DoSomeActionService : IDoSomeActionService
    {
        public Action ActionDelegate { get; set; }

        public DoSomeActionService() : this(() => { })
        {
            
        }
        public DoSomeActionService(Action action)
        {
            this.ActionDelegate = action;
        }

        public void Action()
        {
            ActionDelegate();
        }
    }
}