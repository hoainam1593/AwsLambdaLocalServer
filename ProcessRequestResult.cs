
public class ProcessRequestResult
{
    public string funcName;
    public string playerId;
    public string payload;
    public Dictionary<string, string> environmentVariables;
    public bool isOk;
    public string errMsg;

    public static ProcessRequestResult CreateSuccessObj(string funcName,
        string playerId, string payload, Dictionary<string, string> environmentVariables)
    {
        return new ProcessRequestResult()
        {
            isOk = true,
            funcName = funcName,
            playerId = playerId,
            payload = payload,
            environmentVariables = environmentVariables,
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
