namespace CurseTheBeast.Core.Api.FTB;


public class FTBException : Exception
{
    public string Api { get; }
    public string ApiStatus { get; }
    public string? ApiMessage { get; }

    public FTBException(string api, string status, string? message)
        : base($"FTB接口返回了错误 {status}" + (message == null ? "":$": {message}"))
    {
        Api = api;
        ApiStatus = status;
        ApiMessage = message;
    }
}
