namespace Nizam.Api.Common.Errors;

/// <summary>
/// Base type for expected business-rule failures. The <see cref="ProblemDetailsMiddleware"/>
/// translates these into RFC 7807 ProblemDetails responses with a stable <c>errorCode</c>
/// extension so frontends can switch on machine-readable codes instead of parsing French strings.
/// </summary>
/// <remarks>
/// Use this for things like "stock insufficient", "customer required for credit", "duplicate
/// username", etc. — failures the caller can do something about. NOT for programming errors
/// (use exceptions) or NotFound (return 404 directly from the controller).
/// </remarks>
public class DomainException : Exception
{
    /// <summary>Stable machine-readable error code, e.g. <c>ERR_INSUFFICIENT_STOCK</c>.</summary>
    public string ErrorCode { get; }

    /// <summary>HTTP status to map to. Defaults to 400 Bad Request.</summary>
    public int StatusCode { get; }

    public DomainException(string errorCode, string message, int statusCode = StatusCodes.Status400BadRequest)
        : base(message)
    {
        ErrorCode = errorCode;
        StatusCode = statusCode;
    }
}

/// <summary>409 Conflict — operation collides with current state (e.g. insufficient stock).</summary>
public class DomainConflictException : DomainException
{
    public DomainConflictException(string errorCode, string message)
        : base(errorCode, message, StatusCodes.Status409Conflict) { }
}

/// <summary>404 Not Found — used for a domain entity not found, distinct from a missing route.</summary>
public class DomainNotFoundException : DomainException
{
    public DomainNotFoundException(string errorCode, string message)
        : base(errorCode, message, StatusCodes.Status404NotFound) { }
}
