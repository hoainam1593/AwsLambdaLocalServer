using System.Net;
using System.Reflection;
using System.Text;

namespace AwsLambdaLocalServer
{
    internal class Program
    {
        class RequestInfo
        {
            public string funcName;
            public bool isOk;
            public string errMsg;

            public RequestInfo(string funcName)
            {
                this.funcName = funcName;
                isOk = true;
            }

            public RequestInfo(string errMsg, bool indicateErrorRequest)
            {
                this.errMsg = errMsg;
                isOk = false;
            }
        }

        const string hostURL = "http://localhost:1993/";
        const string funcDllPathForTesting = "E:\\unity-projects\\project-8-server\\bin\\Release\\net6.0\\publish\\project-8-server.dll";

        static string funcDllPath;
        static string funcDllFolder;
        static Assembly funcDll;
        static Dictionary<string, object> dicCacheFunc = new Dictionary<string, object>();

        static void Main(string[] args)
        {
            AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;
            LoadFuncDll();
            RunServer();
        }

        #region http server

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
                    var requestInfo = ProcessRequest(context.Request);
                    ReturnResponse(context.Response, requestInfo);
                    Console.WriteLine($"done processing the request");
                }
                Console.Write("\n\n");
            }
        }

        //example of valid url:
        //http://localhost:1993/function?name=TestFunction
        static RequestInfo ProcessRequest(HttpListenerRequest request)
        {
            var path = request.Url.AbsolutePath.Trim('/', '\\');
            if (!path.Equals("function"))
            {
                return new RequestInfo("The request Url is invalid", false);
            }

            var funcName = request.QueryString["name"];
            if (string.IsNullOrEmpty(funcName))
            {
                return new RequestInfo("Can not found the function name in the request Url", false);
            }

            return new RequestInfo(funcName);
        }

        static void ReturnResponse(HttpListenerResponse response, RequestInfo requestInfo)
        {
            string resMsg;
            if (!requestInfo.isOk)
            {
                response.StatusCode = (int)HttpStatusCode.BadRequest;
                resMsg = requestInfo.errMsg;
            }
            else
            {
                resMsg = $"process function {requestInfo.funcName}";
            }
            
            var resMsgAsBytes = Encoding.UTF8.GetBytes(resMsg);

            response.ContentLength64 = resMsgAsBytes.Length;

            var resStream = response.OutputStream;
            resStream.Write(resMsgAsBytes, 0, resMsgAsBytes.Length);
            resStream.Close();
        }

        #endregion

        #region load dll

        static Assembly? OnAssemblyResolve(object? sender, ResolveEventArgs args)
        {
            var fullname = new AssemblyName(args.Name);
            var assemblyPath = Path.Combine(funcDllFolder, $"{fullname.Name}.dll");
            return Assembly.LoadFile(assemblyPath);
        }

        static void LoadFuncDll()
        {
            funcDllPath = funcDllPathForTesting;
            funcDllFolder = Directory.GetParent(funcDllPath).FullName;
            funcDll = Assembly.LoadFile(funcDllPath);
        }

        #endregion
    }
}