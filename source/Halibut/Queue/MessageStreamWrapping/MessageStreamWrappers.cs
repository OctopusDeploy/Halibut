using System;
using System.Collections.Generic;
using System.Linq;

namespace Halibut.Queue.MessageStreamWrapping
{
    public class MessageStreamWrappers
    {
        public IReadOnlyList<IMessageStreamWrapper> Wrappers { get; }

        public MessageStreamWrappers(IList<IMessageStreamWrapper> wrappers)
        {
            Wrappers = wrappers.ToList();
        }

        public MessageStreamWrappers()
        {
            Wrappers = new List<IMessageStreamWrapper>();
        }
    }
}