namespace Halibut.Transport.Observability
{
    public class NoOpControlMessageObserver : IControlMessageObserver
    {
        void IControlMessageObserver.BeforeSendingControlMessage(string controlMessage)
        {
        }

        void IControlMessageObserver.FinishSendingControlMessage(string controlMessage)
        {
        }

        void IControlMessageObserver.WaitingForControlMessage()
        {
        }

        void IControlMessageObserver.ReceivedControlMessage(string controlMessage)
        {
        }
    }
}