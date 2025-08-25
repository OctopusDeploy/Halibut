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

    public class MessageStreamWrappersBuilder
    {
        readonly IList<IMessageStreamWrapper> wrappers = new List<IMessageStreamWrapper>();

        MessageStreamWrappersBuilder()
        {
        }

        public static MessageStreamWrappersBuilder WrapStreamWith(IMessageStreamWrapper wrapper)
        {
            return new MessageStreamWrappersBuilder().AndThenWrapThatWith(wrapper);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="wrapper"></param>
        /// <returns></returns>
        public MessageStreamWrappersBuilder AndThenWrapThatWith(IMessageStreamWrapper wrapper)
        {
            wrappers.Add(wrapper);
            return this;
        }

        public MessageStreamWrappers Build() => new(wrappers);
    }
}