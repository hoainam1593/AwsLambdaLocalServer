using System.Net;
using System.Reflection;
using System.Text.Json;

namespace AwsLambdaLocalServer
{
    internal class Program
    {
        #region data members

        class ProcessRequestResult
        {
            public string funcName;
            public string playerId;
            public string payload;
            public bool isOk;
            public string errMsg;

            public static ProcessRequestResult CreateSuccessObj(string funcName, string playerId, string payload)
            {
                return new ProcessRequestResult()
                {
                    isOk = true,
                    funcName = funcName,
                    playerId = playerId,
                    payload = payload,
                };
            }

            public static ProcessRequestResult CreateFailObj(string errMsg)
            {
                return new ProcessRequestResult()
                {
                    isOk = false,
                    errMsg = errMsg,
                };
            }
        }

        class ExecuteFuncResult
        {
            public string exeFuncResult;
            public HttpStatusCode statusCode;
            public string errMsg;

            public static ExecuteFuncResult CreateSuccessObj(string exeFuncResult)
            {
                return new ExecuteFuncResult()
                {
                    statusCode = HttpStatusCode.OK,
                    exeFuncResult = exeFuncResult,
                };
            }

            public static ExecuteFuncResult CreateFailObj(string errMsg, HttpStatusCode statusCode)
            {
                return new ExecuteFuncResult()
                {
                    statusCode = statusCode,
                    errMsg = errMsg,
                };
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
            LoadFuncDll(args);
            RunServer();
        }

        #region http server

        static void RunServer()
        {
            var listener = new HttpListener();
            listener.Prefixes.Add(hostURL);
            listener.Start();

            Console.WriteLine("--- Lambda local server has been up and running ---");

            while (true)
            {
                var context = listener.GetContext();

                //normally, a web browser will send 2 request:
                // 1: actual request
                // 2: a request used to get favicon i.e. a small icon of website
                if (!context.Request.Url.ToString().Contains("favicon.ico"))
                {
                    var requestResult = ProcessRequest(context.Request);
                    var exeResult = ExecuteFunc(requestResult);
                    ReturnResponse(context.Response, exeResult);
                }
            }
        }

        //example of valid url:
        //http://localhost:1993/function?name=TestFunction
        static ProcessRequestResult ProcessRequest(HttpListenerRequest request)
        {
            var path = request.Url.AbsolutePath.Trim('/', '\\');
            if (!path.Equals("function"))
            {
                return ProcessRequestResult.CreateFailObj("The request Url is invalid");
            }

            var funcName = request.QueryString["name"];
            if (string.IsNullOrEmpty(funcName))
            {
                return ProcessRequestResult.CreateFailObj("Can not found the function name in the request Url");
            }

            if (!request.HasEntityBody)
            {
                return ProcessRequestResult.CreateFailObj("The request dont have body");
            }

            var dicBody = ParseRequestBody(request);
            if (dicBody == null)
            {
                return ProcessRequestResult.CreateFailObj("The request body is invalid");
            }

            if (!dicBody.ContainsKey("playerId") || !dicBody.ContainsKey("payload"))
            {
                return ProcessRequestResult.CreateFailObj("The request body is missing parameters");
            }

            return ProcessRequestResult.CreateSuccessObj(funcName, dicBody["playerId"], dicBody["payload"]);
        }

        static Dictionary<string, string> ParseRequestBody(HttpListenerRequest request)
        {
            using (var stream = request.InputStream)
            {
                using (var reader = new StreamReader(stream))
                {
                    var bodyTxt = reader.ReadToEnd();
                    try
                    {
                        return JsonSerializer.Deserialize<Dictionary<string, string>>(bodyTxt);
                    }
                    catch
                    {
                        return null;
                    }
                }
            }
        }

        static ExecuteFuncResult ExecuteFunc(ProcessRequestResult requestResult)
        {
            if (!requestResult.isOk)
            {
                return ExecuteFuncResult.CreateFailObj(requestResult.errMsg, HttpStatusCode.BadRequest);
            }

            var obj = GetObjectWithType(requestResult.funcName);
            if (obj == null)
            {
                return ExecuteFuncResult.CreateFailObj(
                    $"function {requestResult.funcName} can not be found",
                    HttpStatusCode.BadRequest);
            }

            try
            {
                var context = CreateContextObj(requestResult.playerId);
                var task = (Task<string>)obj.GetType().InvokeMember("ExecuteByLocalServer", 
                    BindingFlags.InvokeMethod, null, obj, new object[] { requestResult.payload, context });
                task.Wait();
                return ExecuteFuncResult.CreateSuccessObj(task.Result);
            }
            catch (AggregateException aggregateException)
            {
                var e = aggregateException.InnerException;
                var type = e.GetType().Name;
                var msg = e.Message;
                var stacktrace = e.StackTrace;

                return ExecuteFuncResult.CreateFailObj(
                    $"execution of function {requestResult.funcName} failed\n{type}: {msg}\n{stacktrace}",
                    HttpStatusCode.InternalServerError);
            }
        }

        static void ReturnResponse(HttpListenerResponse response, ExecuteFuncResult exeResult)
        {
            response.StatusCode = (int)exeResult.statusCode;
            using (var stream = response.OutputStream)
            {
                using (var writer = new StreamWriter(stream))
                {
                    writer.Write(exeResult.GetMsg());
                }
            }
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

        static void LoadFuncDll(string[] args)
        {
            if (args == null || args.Length == 0)
            {
                funcDllPath = funcDllPathForTesting;
            }
            else
            {
                funcDllPath = Path.Combine(Directory.GetCurrentDirectory(), $"{args[0]}.dll");
            }
            
            funcDllFolder = Directory.GetParent(funcDllPath).FullName;
            funcDll = Assembly.LoadFile(funcDllPath);
        }

        #endregion
    }
}