using System.Net;
using System.Reflection;
using System.Text.Json;

public partial class Program
{
    const string hostURL = "http://localhost:1993/";

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

    #region process request

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

        try
        {
            var dicEnvs = JsonSerializer.Deserialize<Dictionary<string, string>>(dicBody["environmentVariables"]);
            return ProcessRequestResult.CreateSuccessObj(funcName,
                dicBody["playerId"], dicBody["payload"], dicEnvs);
        }
        catch
        {
            return ProcessRequestResult.CreateFailObj("The parameters of request body is invalid");
        }
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

    #endregion

    #region execute function

    static ExecuteFuncResult ExecuteFunc(ProcessRequestResult requestResult)
    {
        if (!requestResult.isOk)
        {
            return ExecuteFuncResult.CreateFailObj(requestResult.errMsg, HttpStatusCode.BadRequest);
        }

        var obj = GetFuncObj(requestResult.funcName);
        if (obj == null)
        {
            return ExecuteFuncResult.CreateFailObj(
                $"function {requestResult.funcName} can not be found",
                HttpStatusCode.BadRequest);
        }

        try
        {
            var context = CreateContextObj(requestResult.playerId, requestResult.environmentVariables);
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

    #endregion

    #region response

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
}