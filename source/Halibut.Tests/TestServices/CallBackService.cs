using System;
using System.Threading.Tasks;

namespace Halibut.Tests.TestServices
{
    public class CallBackService : ICallBackService
    {
        public Action CallBack { get; set; }

        public CallBackService()
        {
            CallBack = () => {};
        }

        public void MakeTheCall()
        {
            CallBack();
        }
    }
}