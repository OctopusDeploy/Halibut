using System;
using Halibut.Transport.Observability;

namespace Halibut.Tests.Support
{
    public class FuncControlMessageObserver : IControlMessageObserver
    {
        public Action<string> BeforeSendingControlMessageAction = (_) => { };
        public Action<string> FinishSendingControlMessageAction = (_) => { };
        public Action WaitingForControlMessageAction = () => { };
        public Action<string> ReceivedControlMessageAction = (_) => { };

        void IControlMessageObserver.BeforeSendingControlMessage(string controlMessage)
        {
            BeforeSendingControlMessageAction(controlMessage);
        }

        void IControlMessageObserver.FinishSendingControlMessage(string controlMessage)
        {
            FinishSendingControlMessageAction(controlMessage);
        }

        void IControlMessageObserver.WaitingForControlMessage()
        {
            WaitingForControlMessageAction();
        }

        void IControlMessageObserver.ReceivedControlMessage(string controlMessage)
        {
            ReceivedControlMessageAction(controlMessage);
        }
    }
}