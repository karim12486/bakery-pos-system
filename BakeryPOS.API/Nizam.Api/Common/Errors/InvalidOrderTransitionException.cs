using Nizam.Api.Core.Enums;

namespace Nizam.Api.Common.Errors;

/// <summary>
/// 422 Unprocessable Entity — a caller attempted to move an order between two states that
/// aren't connected in the state machine (e.g. Closed → Open, or Fired → Open without an
/// explicit recall). Includes <c>fromStatus</c> and <c>toStatus</c> as ProblemDetails
/// extensions so the frontend can render a meaningful error or surface the correct override
/// flow (manager PIN, etc.) without parsing strings.
/// </summary>
public sealed class InvalidOrderTransitionException : DomainException
{
    public OrderStatus FromStatus { get; }
    public OrderStatus ToStatus { get; }

    public InvalidOrderTransitionException(OrderStatus from, OrderStatus to)
        : base(
            errorCode: "ERR_INVALID_ORDER_TRANSITION",
            message: $"Order cannot transition from '{from}' to '{to}'.",
            statusCode: StatusCodes.Status422UnprocessableEntity,
            extensions: new Dictionary<string, object?>
            {
                ["fromStatus"] = from.ToString(),
                ["toStatus"] = to.ToString(),
            })
    {
        FromStatus = from;
        ToStatus = to;
    }
}
