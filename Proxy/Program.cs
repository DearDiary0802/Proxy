using System;
using System.IO;
using System.Net;

namespace Proxy
{
    class Program
    {
        public const string LOCALHOST = "127.0.0.1";
        public const int PROXY_PORT = 20000;
        static void Main(string[] args)
        {
            string list = getList("list.conf");
            string[] blackList = list.Trim().Split(new char[] { '\r', '\n' });

            Proxy proxy = new Proxy(IPAddress.Parse(LOCALHOST), PROXY_PORT, blackList);
            Console.WriteLine("Server started");
            proxy.Start();
        }

        static string getList(string path)
        {
            string blacklist = "";
            try
            {
                StreamReader reader = new StreamReader(path, System.Text.Encoding.Default);
                blacklist = reader.ReadToEnd();
            }
            catch
            {
                return blacklist;
            }
            return blacklist;
        }
    }
}
