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
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Halibut.Transport.Proxy.EventArgs;
using Halibut.Transport.Proxy.Exceptions;

namespace Halibut.Transport.Proxy
{
    /// <summary>
    /// Socks5 connection proxy class.  This class implements the Socks5 standard proxy protocol.
    /// </summary>
    /// <remarks>
    /// This implementation supports TCP proxy connections with a Socks v5 server.
    /// </remarks>
    public class Socks5ProxyClient : IProxyClient
    {
        private SocksAuthentication _proxyAuthMethod;

        private const string PROXY_NAME = "SOCKS5";        
        private const int SOCKS5_DEFAULT_PORT = 1080;
        
        private const byte SOCKS5_VERSION_NUMBER = 5;
        private const byte SOCKS5_RESERVED = 0x00;
        private const byte SOCKS5_AUTH_METHOD_NO_AUTHENTICATION_REQUIRED = 0x00;
        private const byte SOCKS5_AUTH_METHOD_GSSAPI = 0x01;
        private const byte SOCKS5_AUTH_METHOD_USERNAME_PASSWORD = 0x02;
        private const byte SOCKS5_AUTH_METHOD_IANA_ASSIGNED_RANGE_BEGIN = 0x03;
        private const byte SOCKS5_AUTH_METHOD_IANA_ASSIGNED_RANGE_END = 0x7f;
        private const byte SOCKS5_AUTH_METHOD_RESERVED_RANGE_BEGIN = 0x80;
        private const byte SOCKS5_AUTH_METHOD_RESERVED_RANGE_END = 0xfe;
        private const byte SOCKS5_AUTH_METHOD_REPLY_NO_ACCEPTABLE_METHODS = 0xff;
        private const byte SOCKS5_CMD_CONNECT = 0x01;
        private const byte SOCKS5_CMD_BIND = 0x02;
        private const byte SOCKS5_CMD_UDP_ASSOCIATE = 0x03;
        private const byte SOCKS5_CMD_REPLY_SUCCEEDED = 0x00;
        private const byte SOCKS5_CMD_REPLY_GENERAL_SOCKS_SERVER_FAILURE = 0x01;
        private const byte SOCKS5_CMD_REPLY_CONNECTION_NOT_ALLOWED_BY_RULESET = 0x02;
        private const byte SOCKS5_CMD_REPLY_NETWORK_UNREACHABLE = 0x03;
        private const byte SOCKS5_CMD_REPLY_HOST_UNREACHABLE = 0x04;
        private const byte SOCKS5_CMD_REPLY_CONNECTION_REFUSED = 0x05;
        private const byte SOCKS5_CMD_REPLY_TTL_EXPIRED = 0x06;
        private const byte SOCKS5_CMD_REPLY_COMMAND_NOT_SUPPORTED = 0x07;
        private const byte SOCKS5_CMD_REPLY_ADDRESS_TYPE_NOT_SUPPORTED = 0x08;
        private const byte SOCKS5_ADDRTYPE_IPV4 = 0x01;
        private const byte SOCKS5_ADDRTYPE_DOMAIN_NAME = 0x03;
        private const byte SOCKS5_ADDRTYPE_IPV6 = 0x04;

        /// <summary>
        /// Authentication itemType.
        /// </summary>
        private enum SocksAuthentication
        {
            /// <summary>
            /// No authentication used.
            /// </summary>
            None,
            /// <summary>
            /// Username and password authentication.
            /// </summary>
            UsernamePassword
        }

        /// <summary>
        /// Create a Socks5 proxy client object.  
        /// </summary>
        /// <param name="proxyHost">Host name or IP address of the proxy server.</param>
        /// <param name="proxyPort">Port used to connect to proxy server.</param>
        /// <param name="proxyUserName">Proxy authentication user name.</param>
        /// <param name="proxyPassword">Proxy authentication password.</param>
        public Socks5ProxyClient(string proxyHost, int proxyPort, string proxyUserName, string proxyPassword)
        {
            if (string.IsNullOrEmpty(proxyHost))
                throw new ArgumentNullException(nameof(proxyHost));

            if (proxyPort <= 0 || proxyPort > 65535)
                throw new ArgumentOutOfRangeException(nameof(proxyPort), "port must be greater than zero and less than 65535");

            if (proxyUserName == null)
                throw new ArgumentNullException(nameof(proxyUserName));

            if (proxyPassword == null)
                throw new ArgumentNullException(nameof(proxyPassword));
            
            ProxyHost = proxyHost;
            ProxyPort = proxyPort;
            ProxyUserName = proxyUserName;
            ProxyPassword = proxyPassword;
        }

        /// <summary>
        /// Gets or sets host name or IP address of the proxy server.
        /// </summary>
        public string ProxyHost { get; set; }

        /// <summary>
        /// Gets or sets port used to connect to proxy server.
        /// </summary>
        public int ProxyPort { get; set; }

        /// <summary>
        /// Gets String representing the name of the proxy. 
        /// </summary>
        /// <remarks>This property will always return the value 'SOCKS5'</remarks>
        public string ProxyName
        {
            get { return PROXY_NAME; }
        }
        
        /// <summary>
        /// Gets or sets proxy authentication user name.
        /// </summary>
        public string ProxyUserName { get; set; }

        /// <summary>
        /// Gets or sets proxy authentication password.
        /// </summary>
        public string ProxyPassword { get; set; }

        /// <summary>
        /// Gets or sets the TcpClient object. 
        /// This property can be set prior to executing CreateConnection to use an existing TcpClient connection.
        /// </summary>
        public TcpClient TcpClient { get; set; }

        Func<TcpClient> _tcpClientFactory = () => new TcpClient();

        public IProxyClient WithTcpClientFactory(Func<TcpClient> tcpClientfactory)
        {
            _tcpClientFactory = tcpClientfactory;
            return this;
        }

        /// <summary>
        /// Creates a remote TCP connection through a proxy server to the destination host on the destination port.
        /// </summary>
        /// <param name="destinationHost">Destination host name or IP address of the destination server.</param>
        /// <param name="destinationPort">Port number to connect to on the destination host.</param>
        /// <param name="timeout">Timeout duration for the Connect attempt.</param>
        /// <returns>
        /// Returns an open TcpClient object that can be used normally to communicate
        /// with the destination server
        /// </returns>
        /// <remarks>
        /// This method creates a connection to the proxy server and instructs the proxy server
        /// to make a pass through connection to the specified destination host on the specified
        /// port.  
        /// </remarks>
        public TcpClient CreateConnection(string destinationHost, int destinationPort, TimeSpan timeout)
        {
            if (string.IsNullOrEmpty(destinationHost))
                throw new ArgumentNullException(nameof(destinationHost));

            if (destinationPort <= 0 || destinationPort > 65535)
                throw new ArgumentOutOfRangeException(nameof(destinationPort), "port must be greater than zero and less than 65535");

            try
            {
                // if we have no connection, create one
                if (TcpClient == null)
                {
                    if (string.IsNullOrEmpty(ProxyHost))
                        throw new ProxyException("ProxyHost property must contain a value.");

                    if (ProxyPort <= 0 || ProxyPort > 65535)
                        throw new ProxyException("ProxyPort value must be greater than zero and less than 65535");

                    //  create new tcp client object to the proxy server
                    TcpClient = _tcpClientFactory();

                    // attempt to open the connection
                    TcpClient.ConnectWithTimeout(ProxyHost, ProxyPort, timeout);
                }

                //  determine which authentication method the client would like to use
                DetermineClientAuthMethod();

                // negotiate which authentication methods are supported / accepted by the server
                NegotiateServerAuthMethod();

                // send a connect command to the proxy server for destination host and port
                SendCommand(SOCKS5_CMD_CONNECT, destinationHost, destinationPort);

                // return the open proxied tcp client object to the caller for normal use
                return TcpClient;
            }
            catch (Exception ex)
            {
                throw new ProxyException(string.Format(CultureInfo.InvariantCulture, "Connection to proxy host {0} on port {1} failed.", Utils.GetHost(TcpClient), Utils.GetPort(TcpClient)), ex);
            }
        }


        private void DetermineClientAuthMethod()
        {
            //  set the authentication itemType used based on values inputed by the user
            if (ProxyUserName != null && ProxyPassword != null)
                _proxyAuthMethod = SocksAuthentication.UsernamePassword;
            else
                _proxyAuthMethod = SocksAuthentication.None;
        }
       
        private void NegotiateServerAuthMethod()
        {
            //  get a reference to the network stream
            var stream = TcpClient.GetStream();

            // SERVER AUTHENTICATION REQUEST
            // The client connects to the server, and sends a version
            // identifier/method selection message:
            //
            //      +----+----------+----------+
            //      |VER | NMETHODS | METHODS  |
            //      +----+----------+----------+
            //      | 1  |    1     | 1 to 255 |
            //      +----+----------+----------+

            var haveUserPass = !string.IsNullOrEmpty(ProxyUserName) &&
                               !string.IsNullOrEmpty(ProxyPassword);

            var authRequest = new List<byte>();
            authRequest.Add(SOCKS5_VERSION_NUMBER);
            if (haveUserPass) {
                authRequest.Add(2);
                authRequest.Add(SOCKS5_AUTH_METHOD_NO_AUTHENTICATION_REQUIRED);
                authRequest.Add(SOCKS5_AUTH_METHOD_USERNAME_PASSWORD);
            } else {
                authRequest.Add(1);
                authRequest.Add(SOCKS5_AUTH_METHOD_NO_AUTHENTICATION_REQUIRED);
            }

            //  send the request to the server specifying authentication types supported by the client.
            stream.Write(authRequest.ToArray(), 0, authRequest.Count);
            
            //  SERVER AUTHENTICATION RESPONSE
            //  The server selects from one of the methods given in METHODS, and
            //  sends a METHOD selection message:
            //
            //     +----+--------+
            //     |VER | METHOD |
            //     +----+--------+
            //     | 1  |   1    |
            //     +----+--------+
            //
            //  If the selected METHOD is X'FF', none of the methods listed by the
            //  client are acceptable, and the client MUST close the connection.
            //
            //  The values currently defined for METHOD are:
            //   * X'00' NO AUTHENTICATION REQUIRED
            //   * X'01' GSSAPI
            //   * X'02' USERNAME/PASSWORD
            //   * X'03' to X'7F' IANA ASSIGNED
            //   * X'80' to X'FE' RESERVED FOR PRIVATE METHODS
            //   * X'FF' NO ACCEPTABLE METHODS

            //  receive the server response 
            var response = new byte[2];
            stream.Read(response, 0, response.Length);

            //  the first byte contains the socks version number (e.g. 5)
            //  the second byte contains the auth method acceptable to the proxy server
            var acceptedAuthMethod = response[1];
            
            // if the server does not accept any of our supported authenication methods then throw an error
            if (acceptedAuthMethod == SOCKS5_AUTH_METHOD_REPLY_NO_ACCEPTABLE_METHODS)
            {
                TcpClient.Close();
                throw new ProxyException("The proxy destination does not accept the supported proxy client authentication methods.");
            }

            // if the server accepts a username and password authentication and none is provided by the user then throw an error
            if (acceptedAuthMethod == SOCKS5_AUTH_METHOD_USERNAME_PASSWORD && _proxyAuthMethod == SocksAuthentication.None)
            {
                TcpClient.Close();
                throw new ProxyException("The proxy destination requires a username and password for authentication.");
            }

            if (acceptedAuthMethod == SOCKS5_AUTH_METHOD_USERNAME_PASSWORD)
            {

                // USERNAME / PASSWORD SERVER REQUEST
                // Once the SOCKS V5 server has started, and the client has selected the
                // Username/Password Authentication protocol, the Username/Password
                // subnegotiation begins.  This begins with the client producing a
                // Username/Password request:
                //
                //       +----+------+----------+------+----------+
                //       |VER | ULEN |  UNAME   | PLEN |  PASSWD  |
                //       +----+------+----------+------+----------+
                //       | 1  |  1   | 1 to 255 |  1   | 1 to 255 |
                //       +----+------+----------+------+----------+

                var credentials = new byte[ProxyUserName.Length + ProxyPassword.Length + 3];
                credentials[0] = 1;
                credentials[1] = (byte)ProxyUserName.Length; 
                Array.Copy(Encoding.ASCII.GetBytes(ProxyUserName), 0, credentials, 2, ProxyUserName.Length); 
                credentials[ProxyUserName.Length + 2] = (byte)ProxyPassword.Length;
                Array.Copy(Encoding.ASCII.GetBytes(ProxyPassword), 0, credentials, ProxyUserName.Length + 3, ProxyPassword.Length); 

                // USERNAME / PASSWORD SERVER RESPONSE
                // The server verifies the supplied UNAME and PASSWD, and sends the
                // following response:
                //
                //   +----+--------+
                //   |VER | STATUS |
                //   +----+--------+
                //   | 1  |   1    |
                //   +----+--------+
                //
                // A STATUS field of X'00' indicates success. If the server returns a
                // `failure' (STATUS value other than X'00') status, it MUST close the
                // connection.
                stream.Write(credentials, 0, credentials.Length);
                var crResponse = new byte[2];
                stream.Read(crResponse, 0, crResponse.Length);

                if (crResponse[1] != 0)
                {
                    TcpClient.Close();
                    throw new ProxyException("Proxy authentification failure!");
                }
            }
        }

        private byte GetDestAddressType(string host)
        {
            IPAddress ipAddr = null;

            var result = IPAddress.TryParse(host, out ipAddr);

            if (!result) 
                return SOCKS5_ADDRTYPE_DOMAIN_NAME;

            switch (ipAddr.AddressFamily)
            {
                case AddressFamily.InterNetwork:
                    return SOCKS5_ADDRTYPE_IPV4;
                case AddressFamily.InterNetworkV6:
                    return SOCKS5_ADDRTYPE_IPV6;
                default:
                    throw new ProxyException(string.Format(CultureInfo.InvariantCulture, "The host addess {0} of type '{1}' is not a supported address type.  The supported types are InterNetwork and InterNetworkV6.", host, Enum.GetName(typeof(AddressFamily), ipAddr.AddressFamily)));
            }
            
        }

        private byte[] GetDestAddressBytes(byte addressType, string host)
        {
            switch (addressType)
            {
                case SOCKS5_ADDRTYPE_IPV4:
                case SOCKS5_ADDRTYPE_IPV6:
                    return IPAddress.Parse(host).GetAddressBytes();
                case SOCKS5_ADDRTYPE_DOMAIN_NAME:
                    //  create a byte array to hold the host name bytes plus one byte to store the length
                    var bytes = new byte[host.Length + 1]; 
                    //  if the address field contains a fully-qualified domain name.  The first
                    //  octet of the address field contains the number of octets of name that
                    //  follow, there is no terminating NUL octet.
                    bytes[0] = Convert.ToByte(host.Length);
                    Encoding.ASCII.GetBytes(host).CopyTo(bytes, 1);
                    return bytes;
                default:
                    return null;
            }
        }

        private byte[] GetDestPortBytes(int value)
        {
            var array = new byte[2];
            array[0] = Convert.ToByte(value / 256);
            array[1] = Convert.ToByte(value % 256);
            return array;
        }

        private void SendCommand(byte command, string destinationHost, int destinationPort)
        {
            var stream = TcpClient.GetStream();

            var addressType = GetDestAddressType(destinationHost);
            var destAddr = GetDestAddressBytes(addressType, destinationHost);
            var destPort = GetDestPortBytes(destinationPort);

            //  The connection request is made up of 6 bytes plus the
            //  length of the variable address byte array
            //
            //  +----+-----+-------+------+----------+----------+
            //  |VER | CMD |  RSV  | ATYP | DST.ADDR | DST.PORT |
            //  +----+-----+-------+------+----------+----------+
            //  | 1  |  1  | X'00' |  1   | Variable |    2     |
            //  +----+-----+-------+------+----------+----------+
            //
            // * VER protocol version: X'05'
            // * CMD
            //   * CONNECT X'01'
            //   * BIND X'02'
            //   * UDP ASSOCIATE X'03'
            // * RSV RESERVED
            // * ATYP address itemType of following address
            //   * IP V4 address: X'01'
            //   * DOMAINNAME: X'03'
            //   * IP V6 address: X'04'
            // * DST.ADDR desired destination address
            // * DST.PORT desired destination port in network octet order            

            var request = new byte[4 + destAddr.Length + 2];
            request[0] = SOCKS5_VERSION_NUMBER;
            request[1] = command;
            request[2] = SOCKS5_RESERVED;
            request[3] = addressType;
            destAddr.CopyTo(request, 4);
            destPort.CopyTo(request, 4 + destAddr.Length);
            
            // send connect request.
            stream.Write(request, 0, request.Length);
        
            //  PROXY SERVER RESPONSE
            //  +----+-----+-------+------+----------+----------+
            //  |VER | REP |  RSV  | ATYP | BND.ADDR | BND.PORT |
            //  +----+-----+-------+------+----------+----------+
            //  | 1  |  1  | X'00' |  1   | Variable |    2     |
            //  +----+-----+-------+------+----------+----------+
            //
            // * VER protocol version: X'05'
            // * REP Reply field:
            //   * X'00' succeeded
            //   * X'01' general SOCKS server failure
            //   * X'02' connection not allowed by ruleset
            //   * X'03' Network unreachable
            //   * X'04' Host unreachable
            //   * X'05' Connection refused
            //   * X'06' TTL expired
            //   * X'07' Command not supported
            //   * X'08' Address itemType not supported
            //   * X'09' to X'FF' unassigned
            //* RSV RESERVED
            //* ATYP address itemType of following address

            var response = new byte[255];
            
            // read proxy server response
            var responseSize = stream.Read(response, 0, response.Length);
            
            var replyCode = response[1];

            //  evaluate the reply code for an error condition
            if (responseSize < 2  || replyCode != SOCKS5_CMD_REPLY_SUCCEEDED)
                HandleProxyCommandError(response, destinationHost, destinationPort );
        }

        private void HandleProxyCommandError(byte[] response, string destinationHost, int destinationPort)
        {
            string proxyErrorText;
            var replyCode = response[1];
            var addrType = response[3];
            var addr = "";
            short port = 0;

            switch (addrType)
            {
                case SOCKS5_ADDRTYPE_DOMAIN_NAME:
                    var addrLen = Convert.ToInt32(response[4]);
                    var addrBytes = new byte[addrLen];
                    for (var i = 0; i < addrLen; i++)
                        addrBytes[i] = response[i + 5];
                    addr = Encoding.ASCII.GetString(addrBytes);
                    var portBytesDomain = new byte[2];
                    portBytesDomain[0] = response[6 + addrLen];
                    portBytesDomain[1] = response[5 + addrLen];
                    port = BitConverter.ToInt16(portBytesDomain, 0); 
                    break;

                case SOCKS5_ADDRTYPE_IPV4:
                    var ipv4Bytes = new byte[4];
                    for (var i = 0; i < 4; i++)
                        ipv4Bytes[i] = response[i + 4];
                    var ipv4 = new IPAddress(ipv4Bytes);
                    addr = ipv4.ToString();
                    var portBytesIpv4 = new byte[2];
                    portBytesIpv4[0] = response[9];
                    portBytesIpv4[1] = response[8];
                    port = BitConverter.ToInt16(portBytesIpv4, 0); 
                    break;

                case SOCKS5_ADDRTYPE_IPV6:
                    var ipv6Bytes = new byte[16];
                    for (var i = 0; i < 16; i++)
                        ipv6Bytes[i] = response[i + 4];
                    var ipv6 = new IPAddress(ipv6Bytes);
                    addr = ipv6.ToString();
                    var portBytesIpv6 = new byte[2];
                    portBytesIpv6[0] = response[21];
                    portBytesIpv6[1] = response[20];
                    port = BitConverter.ToInt16(portBytesIpv6, 0); 
                    break;
            }


            switch (replyCode)
            {
                case SOCKS5_CMD_REPLY_GENERAL_SOCKS_SERVER_FAILURE:
                    proxyErrorText = "a general socks destination failure occurred";
                    break;
                case SOCKS5_CMD_REPLY_CONNECTION_NOT_ALLOWED_BY_RULESET:
                    proxyErrorText = "the connection is not allowed by proxy destination rule set";
                    break;
                case SOCKS5_CMD_REPLY_NETWORK_UNREACHABLE:
                    proxyErrorText = "the network was unreachable";
                    break;
                case SOCKS5_CMD_REPLY_HOST_UNREACHABLE:
                    proxyErrorText = "the host was unreachable";
                    break;
                case SOCKS5_CMD_REPLY_CONNECTION_REFUSED:
                    proxyErrorText = "the connection was refused by the remote network";
                    break;
                case SOCKS5_CMD_REPLY_TTL_EXPIRED:
                    proxyErrorText = "the time to live (TTL) has expired";
                    break;
                case SOCKS5_CMD_REPLY_COMMAND_NOT_SUPPORTED:
                    proxyErrorText = "the command issued by the proxy client is not supported by the proxy destination";
                    break;
                case SOCKS5_CMD_REPLY_ADDRESS_TYPE_NOT_SUPPORTED:
                    proxyErrorText = "the address type specified is not supported";
                    break;
                default:
                    proxyErrorText = string.Format(CultureInfo.InvariantCulture, "that an unknown reply with the code value '{0}' was received by the destination", replyCode.ToString(CultureInfo.InvariantCulture));
                    break;
            }
            var exceptionMsg = string.Format(CultureInfo.InvariantCulture, "The {0} concerning destination host {1} port number {2}.  The destination reported the host as {3} port {4}.", proxyErrorText, destinationHost, destinationPort, addr, port.ToString(CultureInfo.InvariantCulture));

            throw new ProxyException(exceptionMsg);

        }
    }
}
