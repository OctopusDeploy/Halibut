using System;

namespace Halibut.Transport.Protocol
{
    public class RemoteIdentity
    {
        public RemoteIdentity(RemoteIdentityType identityType, Version version, Uri subscriptionId)
        {
            this.IdentityType = identityType;
            this.Version = version;
            this.SubscriptionId = subscriptionId;
        }

        public RemoteIdentity(RemoteIdentityType identityType, Version version)
        {
            this.IdentityType = identityType;
            this.Version = version;
        }

        public RemoteIdentityType IdentityType { get; }

        public Uri SubscriptionId { get; }

        public Version Version { get; }
    }
}