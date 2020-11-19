using System;
using System.IO;
using FluentAssertions;
using Halibut.Diagnostics;
using Halibut.Transport.Protocol;
using NSubstitute;
using NUnit.Framework;

namespace Halibut.Tests
{
    public class MessageExchangeStreamFixture
    {
        readonly ILog nullLog = Substitute.For<ILog>();
        
        [Test]
        public void AStringCanBeRoundTripped()
        {
            WithLinkedStreams((a, b) =>
            {
                a.Send("This is the message");
                b.Receive<string>().Should().Be("This is the message");
            });
        }

        [Test]
        public void ALongCanBeRoundTripped()
        {
            WithLinkedStreams((a, b) =>
            {
                a.Send(4L);
                b.Receive<long>().Should().Be(4);
            });
        }

        [Test]
        public void AnObjectCanBeRoundTripped()
        {
            WithLinkedStreams((a, b) =>
            {
                a.Send(new Person{ Name = "Octobob", Age = 30 });
                var actual = b.Receive<Person>();

                actual.Name.Should().Be("Octobob");
                actual.Age.Should().Be(30);
            });
        }

        [Test]
        public void AnExceptionIsThrownIfEndIsReceivedUnexpectedly()
        {
            WithLinkedStreams((a, b) =>
            {
                a.SendEnd();
                b.Invoking(x => x.Receive<string>())
                    .ShouldThrow<HalibutClientException>()
                    .WithMessage("Connection ended by remote. This can occur if the remote shut down while a request was in process.");
            });
        }

        [Test]
        public void AnExceptionIsThrownIfAControlMessageIsReceivedUnexpectedly()
        {
            WithLinkedStreams((a, b) =>
            {
                a.SendNext();
                b.Invoking(x => x.Receive<string>())
                    .ShouldThrow<HalibutClientException>()
                    .WithMessage("Data format error: expected deflated bson message, but got control message 'NEXT'");
            });
        }

        /// <summary>
        /// Creates two streams, where writing to one end writes the bytes into the buffer
        /// of the other stream. Simulates a pair of connected network streams.
        /// </summary>
        void WithLinkedStreams(Action<MessageExchangeStream, MessageExchangeStream> action)
        {
            var underlyingStream1 = new LinkedStream();
            var underlyingStream2 = new LinkedStream();

            underlyingStream1.Other = underlyingStream2;
            underlyingStream2.Other = underlyingStream1;

            using (underlyingStream1)
            using (underlyingStream2)
            {
                var mxStream1 = new MessageExchangeStream(underlyingStream1, nullLog);
                var mxStream2 = new MessageExchangeStream(underlyingStream2, nullLog);
                action(mxStream1, mxStream2);
            }
        }

        class Person
        {
            public string Name { get; set; }
            
            public int Age { get; set; }
        }
        
        class LinkedStream : Stream
        {
            public LinkedStream Other = null;

            readonly MemoryStream inner = new MemoryStream();
            
            public override void Flush()
            {
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                return inner.Seek(offset, origin);
            }

            public override void SetLength(long value)
            {
                inner.SetLength(value);
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                return inner.Read(buffer, offset, count);
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                var pos = Other.inner.Position;
                Other.inner.Write(buffer, offset, count);
                Other.inner.Position = pos;
            }

            public override bool CanRead => true;
            public override bool CanSeek => true;
            public override bool CanWrite => true;
            public override long Length => inner.Length;

            public override long Position
            {
                get => inner.Position;
                set => inner.Position = value;
            }

            protected override void Dispose(bool disposing)
            {
                inner?.Dispose();
            }
        }
    }
}