using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace Proxy
{
    class Proxy
    {
        public string[] blacklist;
        public const int BACKLOG = 50;
        public const int BUFFER_SIZE = 50 * 1024;
        IPAddress IPAddress;
        int Port;
        Socket socket;
        public Proxy(IPAddress ip, int port, string[] blackList)
        {
            blacklist = blackList;
            IPAddress = ip;
            Port = port;
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socket.Bind(new IPEndPoint(IPAddress, Port));
        }
        public void Start()
        {
            socket.Listen(BACKLOG);
            while (true)
            {
                Socket newSocket = socket.Accept();
                Thread thread = new Thread(() => ProxyWork(newSocket));
                thread.Start();
            }
        }
        public void ProxyWork(Socket newSocket)
        {
            NetworkStream networkStream = new NetworkStream(newSocket);
            string message = Encoding.UTF8.GetString(Receive(networkStream));
            ResponseFromProxy(networkStream, message);
            newSocket.Dispose();
        }
        public byte[] Receive(NetworkStream networkStream)
        {
            byte[] buffer = new byte[BUFFER_SIZE];
            byte[] data = new byte[BUFFER_SIZE];
            int reciveBytes = 0;
            int size;
            do
            {
                size = networkStream.Read(buffer, 0, buffer.Length);
                Array.Copy(buffer, 0, data, reciveBytes, size);
                reciveBytes += size;
            } while (networkStream.DataAvailable && reciveBytes < BUFFER_SIZE);
            return data;
        }
        public void ResponseFromProxy(NetworkStream networkStream, string message)
        {
            Socket server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                message = GetRelativePath(message);
                string[] stringInMessage = message.Split('\r', '\n');
                string host = stringInMessage.FirstOrDefault((str) => str.Contains("Host: "));
                host = host.Remove(host.IndexOf("Host: "), ("Host: ").Length);

                if (blacklist != null && Array.IndexOf(blacklist, host.ToLower()) != -1)
                {
                    string error = $"HTTP/1.1 403 Forbidden\r\nContent-Type: text/html\r\nContent-Length: 40\r\n\r\nAccess denied. This syte is in blacklist";
                    byte[] errorpage = Encoding.UTF8.GetBytes(error);
                    networkStream.Write(errorpage, 0, errorpage.Length);
                    Console.WriteLine(DateTime.Now + ": " + host + " 403 (blocked)");
                    return;
                }

                string[] DomainAndPort = host.Split(':');
                IPAddress hostIP = Dns.GetHostEntry(DomainAndPort[0]).AddressList[0];
                IPEndPoint serverEP;
                if (DomainAndPort.Length == 2)
                {
                    serverEP = new IPEndPoint(hostIP, int.Parse(DomainAndPort[1]));
                }
                else
                {
                    serverEP = new IPEndPoint(hostIP, 80);
                }
                server.Connect(serverEP);
                NetworkStream serverStream = new NetworkStream(server);                
                byte[] messageBytes = Encoding.UTF8.GetBytes(message);
                serverStream.Write(messageBytes, 0, messageBytes.Length);
                byte[] receiveData = Receive(serverStream);
                networkStream.Write(receiveData, 0, receiveData.Length);

                string code = GetResponseCode(Encoding.UTF8.GetString(receiveData));
                Console.WriteLine(DateTime.Now.ToString() + " Host: {0} code: {1}", DomainAndPort[0], code);
                serverStream.CopyTo(networkStream);
            }
            catch
            {
                return;
            }
            finally
            {
                server.Dispose();
            }
        }
        public string GetRelativePath(string message)
        {
            MatchCollection matchCollection = (new Regex(@"http:\/\/[a-z0-9а-я\.\:]*")).Matches(message);
            string host = matchCollection[0].Value;
            message = message.Replace(host, "");
            return message;
        }
        public string GetResponseCode(string serverResponse)
        {
            string[] response = serverResponse.Split('\r', '\n');
            string code = response[0].Substring(response[0].IndexOf(" ") + 1);

            return code;
        }
    }
}
