using System.Net;
using System.Text;

namespace AwsLambdaLocalServer
{
    internal class Program
    {
        const string hostURL = "http://localhost:1993/";

        static void Main(string[] args)
        {
            RunServer();
        }

        static void RunServer()
        {
            var listener = new HttpListener();
            listener.Prefixes.Add(hostURL);
            listener.Start();

            while (true)
            {
                Console.WriteLine("=================================================");
                Console.WriteLine("==       server is listening requests...       ==");
                Console.WriteLine("=================================================");
                var context = listener.GetContext();

                //normally, a web browser will send 2 request:
                // 1: actual request
                // 2: a request used to get favicon i.e. a small icon of website
                if (context.Request.Url.ToString().Contains("favicon.ico"))
                {
                    Console.WriteLine($"skipped trash request");
                }
                else
                {
                    Console.WriteLine($"there's a incoming request");
                    ProcessRequest(context.Request, context.Response);
                    Console.WriteLine($"done processing the request");
                }
                Console.Write("\n\n");
            }
        }

        static void ProcessRequest(HttpListenerRequest request, HttpListenerResponse response)
        {
            var resMsg = "hello from server";
            var resMsgAsBytes = Encoding.UTF8.GetBytes(resMsg);

            response.ContentLength64 = resMsgAsBytes.Length;

            var resStream = response.OutputStream;
            resStream.Write(resMsgAsBytes, 0, resMsgAsBytes.Length);
            resStream.Close();
        }
    }
}