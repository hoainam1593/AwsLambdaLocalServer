using System.Net;
using System.Reflection;
using System.Text;

namespace AwsLambdaLocalServer
{
    internal class Program
    {
        #region data members

        class ProcessRequestResult
        {
            public string funcName;
            public bool isOk;
            public string errMsg;

            public ProcessRequestResult(string funcName)
            {
                this.funcName = funcName;
                isOk = true;
            }

            public ProcessRequestResult(string errMsg, bool indicateErrorRequest)
            {
                this.errMsg = errMsg;
                isOk = false;
            }
        }

        class ExecuteFuncResult
        {
            public string exeFuncResult;
            public HttpStatusCode statusCode;
            public string errMsg;

            public ExecuteFuncResult(ProcessRequestResult requestResult)
            {
                statusCode = HttpStatusCode.BadRequest;
                errMsg = requestResult.errMsg;
            }

            public ExecuteFuncResult(string exeFuncResult)
            {
                this.exeFuncResult = exeFuncResult;
                statusCode = HttpStatusCode.OK;
            }

            public ExecuteFuncResult(string errMsg, HttpStatusCode statusCode)
            {
                this.statusCode = statusCode;
                this.errMsg = errMsg;
            }

            public string GetMsg()
            {
                if (statusCode == HttpStatusCode.OK)
                {
                    return exeFuncResult;
                }
                else
                {
                    return errMsg;
                }
            }
        }

        const string hostURL = "http://localhost:1993/";
        const string funcDllPathForTesting = "E:\\unity-projects\\project-8-server\\bin\\Release\\net6.0\\publish\\project-8-server.dll";

        static string funcDllPath;
        static string funcDllFolder;
        static Assembly funcDll;
        static Dictionary<string, object> dicCacheFunc = new Dictionary<string, object>();

        #endregion

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
                    var requestResult = ProcessRequest(context.Request);
                    var exeResult = ExecuteFunc(requestResult);
                    ReturnResponse(context.Response, exeResult);
                    Console.WriteLine($"done processing the request");
                }
                Console.Write("\n\n");
            }
        }

        //example of valid url:
        //http://localhost:1993/function?name=TestFunction
        static ProcessRequestResult ProcessRequest(HttpListenerRequest request)
        {
            var path = request.Url.AbsolutePath.Trim('/', '\\');
            if (!path.Equals("function"))
            {
                return new ProcessRequestResult("The request Url is invalid", false);
            }

            var funcName = request.QueryString["name"];
            if (string.IsNullOrEmpty(funcName))
            {
                return new ProcessRequestResult("Can not found the function name in the request Url", false);
            }

            return new ProcessRequestResult(funcName);
        }

        static ExecuteFuncResult ExecuteFunc(ProcessRequestResult requestResult)
        {
            if (!requestResult.isOk)
            {
                return new ExecuteFuncResult(requestResult);
            }

            var obj = GetObjectWithType(requestResult.funcName);
            if (obj == null)
            {
                return new ExecuteFuncResult(
                    $"function {requestResult.funcName} can not be found",
                    HttpStatusCode.BadRequest);
            }

            try
            {
                var playerId = "";
                var input = "nam";
                var context = CreateContextObj(playerId);
                var task = (Task<string>)obj.GetType().InvokeMember(
                    "Execute", BindingFlags.InvokeMethod, null, obj, new object[] { input, context });
                task.Wait();
                return new ExecuteFuncResult(task.Result);
            }
            catch
            {
                return new ExecuteFuncResult(
                    $"execution of function {requestResult.funcName} failed",
                    HttpStatusCode.InternalServerError);
            }
        }

        static void ReturnResponse(HttpListenerResponse response, ExecuteFuncResult exeResult)
        {
            response.StatusCode = (int)exeResult.statusCode;

            string resMsg = exeResult.GetMsg();
            var resMsgAsBytes = Encoding.UTF8.GetBytes(resMsg);

            response.ContentLength64 = resMsgAsBytes.Length;

            var resStream = response.OutputStream;
            resStream.Write(resMsgAsBytes, 0, resMsgAsBytes.Length);
            resStream.Close();
        }

        #endregion

        #region load dll

        static object CreateContextObj(string playerId)
        {
            var type = funcDll.GetType("LocalServerContext");
            var ctor = type.GetConstructor(new[] { typeof(string) });
            return ctor.Invoke(new object[] { playerId });
        }

        static object GetObjectWithType(string typeName)
        {
            try
            {
                if (!dicCacheFunc.ContainsKey(typeName))
                {
                    var type = funcDll.GetType(typeName);
                    var obj = Activator.CreateInstance(type);
                    dicCacheFunc.Add(typeName, obj);
                }
                return dicCacheFunc[typeName];
            }
            catch
            {
                return null;
            }
        }

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