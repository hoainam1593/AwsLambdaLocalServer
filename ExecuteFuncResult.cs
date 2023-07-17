
using System.Net;

public class ExecuteFuncResult
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
