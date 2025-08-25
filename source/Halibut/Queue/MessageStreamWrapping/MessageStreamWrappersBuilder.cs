using System;
using System.Collections.Generic;

namespace Halibut.Queue.MessageStreamWrapping
{
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