/*
 *  Authors:  Benton Stark
 * 
 *  Copyright (c) 2007-2009 Starksoft, LLC (http://www.starksoft.com) 
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in
 * all copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
 * THE SOFTWARE.
 * 
 */

using System;
using System.Runtime.Serialization;

namespace Halibut.Transport.Proxy.Exceptions
{

    /// <summary>
    /// This exception is thrown when a general, unexpected proxy error.   
    /// </summary>
    [Serializable()]
    public class ProxyException : Exception
    {

        public bool CausedByNetworkError { get; protected set; }
        
        /// <summary>
        /// Constructor.
        /// </summary>
        public ProxyException()
        {
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="message">Exception message text.</param>
        public ProxyException(string message, bool causedByNetworkError)
            : base(message)
        {
            this.CausedByNetworkError = causedByNetworkError;
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="message">Exception message text.</param>
        /// <param name="innerException">The inner exception object.</param>
        public ProxyException(string message, Exception innerException, bool causedByNetworkError)
            :
           base(message, innerException)
        {
            CausedByNetworkError = causedByNetworkError;
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="info">Serialization information.</param>
        /// <param name="context">Stream context information.</param>
        protected ProxyException(SerializationInfo info, StreamingContext context)
#pragma warning disable SYSLIB0051
            : base(info, context)
#pragma warning restore SYSLIB0051
        {
        }
    }

}