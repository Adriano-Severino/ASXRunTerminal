namespace ASXRunTerminal.Core;

internal sealed class McpRequestException : Exception
{
    public McpRequestException(
        string method,
        int code,
        string message,
        string? rawData = null)
        : base(message)
    {
        Method = method;
        Code = code;
        RawData = rawData;
    }

    public string Method { get; }

    public int Code { get; }

    public string? RawData { get; }
}
