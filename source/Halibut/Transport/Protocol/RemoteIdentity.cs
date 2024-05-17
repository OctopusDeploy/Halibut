using System;

namespace Halibut.Transport.Protocol
{
    public class RemoteIdentity
    {
        public RemoteIdentity(RemoteIdentityType identityType, Uri subscriptionId)
        {
            IdentityType = identityType;
            SubscriptionId = subscriptionId;
        }

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        public RemoteIdentity(RemoteIdentityType identityType)
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        {
            IdentityType = identityType;
        }

        public RemoteIdentityType IdentityType { get; }

        public Uri SubscriptionId { get; }
    }
}