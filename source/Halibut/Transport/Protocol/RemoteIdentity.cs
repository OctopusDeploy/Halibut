using System;

namespace Halibut.Transport.Protocol
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

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        public RemoteIdentity(RemoteIdentityType identityType)
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
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