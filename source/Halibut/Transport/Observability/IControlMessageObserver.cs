
namespace Halibut.Transport.Observability
{
    public interface IControlMessageObserver
    {
        internal void BeforeSendingControlMessage(string controlMessage);
        internal void FinishSendingControlMessage(string controlMessage);
        internal void WaitingForControlMessage();
        internal void ReceivedControlMessage(string controlMessage);
    }
}