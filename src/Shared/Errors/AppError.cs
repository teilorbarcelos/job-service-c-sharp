namespace JobService.Shared.Errors;

public class AppError : Exception
{
    public string Code { get; }
    public int StatusCode { get; }

    public AppError(string code, string message, int statusCode = 500) : base(message)
    {
        Code = code;
        StatusCode = statusCode;
    }
}

public sealed class ConfigurationError : AppError
{
    public ConfigurationError(string message)
        : base("CONFIGURATION_ERROR", message, 500) { }
}

public sealed class ValidationError : AppError
{
    public ValidationError(string message)
        : base("VALIDATION_ERROR", message, 400) { }
}

public sealed class ConnectionError : AppError
{
    public ConnectionError(string service, string message)
        : base("CONNECTION_ERROR", $"{service}: {message}", 503) { }
}
