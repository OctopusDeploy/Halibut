using System;

namespace Halibut.ServiceModel
{
    public interface IRemoteServiceAgent
    {
        bool ProcessNext();
    }
}