using System;
using Halibut.Transport.Proxy;
using Newtonsoft.Json;

namespace Halibut
{
    public class ProxyDetails : IEquatable<ProxyDetails>
    {
        public ProxyDetails(string host, int port, ProxyType type)
        {
            this.Host = host;
            this.Port = port;
            this.Type = type;
        }

        [JsonConstructor]
        public ProxyDetails(string host, int port, ProxyType type, string userName, string password)
            : this(host, port, type)
        {
            this.UserName = userName;
            this.Password = password;
        }

        public string Host { get; }

        public int Port { get; }

        public string UserName { get; }

        public string Password { get; }

        public ProxyType Type { get; }

        public bool Equals(ProxyDetails other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return string.Equals(Host, other.Host) && Port == other.Port && string.Equals(UserName, other.UserName) && string.Equals(Password, other.Password) && Type == other.Type;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((ProxyDetails) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (Host != null ? Host.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ Port;
                hashCode = (hashCode*397) ^ (UserName != null ? UserName.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ (Password != null ? Password.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ (int) Type;
                return hashCode;
            }
        }

        public static bool operator ==(ProxyDetails left, ProxyDetails right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(ProxyDetails left, ProxyDetails right)
        {
            return !Equals(left, right);
        }
    }
}