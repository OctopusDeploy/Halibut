using System;

namespace Halibut.Services
{
    public class RemoteIdentity
    {
        readonly RemoteIdentityType identityType;
        readonly Uri subscriptionId;

        public RemoteIdentity(RemoteIdentityType identityType, Uri subscriptionId)
        {
            this.identityType = identityType;
            this.subscriptionId = subscriptionId;
        }

        public RemoteIdentity(RemoteIdentityType identityType)
        {
            this.identityType = identityType;
        }

        public RemoteIdentityType IdentityType
        {
            get { return identityType; }
        }

        public Uri SubscriptionId
        {
            get { return subscriptionId; }
        }
    }
}