using System;
using System.Threading;

namespace Octopus.Tentacle.Contracts
{
    public class ScriptTicket : IEquatable<ScriptTicket>
    {
        public ScriptTicket(string taskId)
        {
            TaskId = taskId ?? throw new ArgumentNullException(nameof(taskId));
        }

        public string TaskId { get; }

        public bool Equals(ScriptTicket? other)
        {
            if (ReferenceEquals(null, other))
                return false;
            if (ReferenceEquals(this, other))
                return true;
            return string.Equals(TaskId, other.TaskId);
        }

        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(null, obj))
                return false;
            if (ReferenceEquals(this, obj))
                return true;
            if (obj.GetType() != GetType())
                return false;
            return Equals((ScriptTicket)obj);
        }

        public override int GetHashCode()
            => TaskId != null ? TaskId.GetHashCode() : 0;

        public static bool operator ==(ScriptTicket left, ScriptTicket right)
            => Equals(left, right);

        public static bool operator !=(ScriptTicket left, ScriptTicket right)
            => !Equals(left, right);

        public override string ToString()
            => TaskId;
    }
}