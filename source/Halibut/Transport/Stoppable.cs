using System.Threading.Tasks;

namespace Halibut.Transport
{
    public interface Stoppable
    {
        Task Stop();
    }
}