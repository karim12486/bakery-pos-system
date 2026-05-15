using Nizam.Api.Common.Errors;
using Nizam.Api.Core.Enums;
using Nizam.Api.Services.Orders;

namespace Nizam.Api.Tests.Services;

/// <summary>
/// Unit tests for <see cref="OrderStateMachine"/>. Pure logic — covers every edge of the
/// transition graph + the same-state no-op + the terminal-state lockdown.
/// </summary>
public class OrderStateMachineTests
{
    private readonly IOrderStateMachine _sm = new OrderStateMachine();

    // ----- Allowed transitions ------------------------------------------------------

    [Theory]
    [InlineData(OrderStatus.Open, OrderStatus.Fired)]
    [InlineData(OrderStatus.Open, OrderStatus.Served)]
    [InlineData(OrderStatus.Open, OrderStatus.Closed)]
    [InlineData(OrderStatus.Open, OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Fired, OrderStatus.Served)]
    [InlineData(OrderStatus.Fired, OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Served, OrderStatus.Closed)]
    [InlineData(OrderStatus.Served, OrderStatus.Cancelled)]
    public void CanTransition_ReturnsTrue_ForPermittedEdges(OrderStatus from, OrderStatus to)
    {
        Assert.True(_sm.CanTransition(from, to));
        _sm.AssertTransition(from, to); // should not throw
    }

    // ----- Same-state no-op ---------------------------------------------------------

    [Theory]
    [InlineData(OrderStatus.Open)]
    [InlineData(OrderStatus.Fired)]
    [InlineData(OrderStatus.Served)]
    [InlineData(OrderStatus.Closed)]
    [InlineData(OrderStatus.Cancelled)]
    public void CanTransition_SameState_IsNoOp(OrderStatus state)
    {
        Assert.True(_sm.CanTransition(state, state));
        _sm.AssertTransition(state, state);
    }

    // ----- Terminal states ----------------------------------------------------------

    [Theory]
    [InlineData(OrderStatus.Closed, OrderStatus.Open)]
    [InlineData(OrderStatus.Closed, OrderStatus.Fired)]
    [InlineData(OrderStatus.Closed, OrderStatus.Served)]
    [InlineData(OrderStatus.Closed, OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Cancelled, OrderStatus.Open)]
    [InlineData(OrderStatus.Cancelled, OrderStatus.Fired)]
    [InlineData(OrderStatus.Cancelled, OrderStatus.Served)]
    [InlineData(OrderStatus.Cancelled, OrderStatus.Closed)]
    public void Terminal_State_Cannot_Transition(OrderStatus from, OrderStatus to)
    {
        Assert.False(_sm.CanTransition(from, to));
        Assert.Throws<InvalidOrderTransitionException>(() => _sm.AssertTransition(from, to));
    }

    // ----- Backwards / illegal transitions ------------------------------------------

    [Theory]
    [InlineData(OrderStatus.Fired, OrderStatus.Open)]      // can't unfire
    [InlineData(OrderStatus.Fired, OrderStatus.Closed)]    // must go through Served
    [InlineData(OrderStatus.Served, OrderStatus.Open)]     // can't reopen
    [InlineData(OrderStatus.Served, OrderStatus.Fired)]    // can't re-fire
    public void Illegal_Transitions_Throw(OrderStatus from, OrderStatus to)
    {
        Assert.False(_sm.CanTransition(from, to));
        Assert.Throws<InvalidOrderTransitionException>(() => _sm.AssertTransition(from, to));
    }

    // ----- Exception payload --------------------------------------------------------

    [Fact]
    public void InvalidTransitionException_CarriesFromAndToInExtensions()
    {
        var ex = Assert.Throws<InvalidOrderTransitionException>(
            () => _sm.AssertTransition(OrderStatus.Closed, OrderStatus.Open));

        Assert.Equal(OrderStatus.Closed, ex.FromStatus);
        Assert.Equal(OrderStatus.Open, ex.ToStatus);
        Assert.Equal(422, ex.StatusCode);
        Assert.Equal("ERR_INVALID_ORDER_TRANSITION", ex.ErrorCode);
        Assert.NotNull(ex.Extensions);
        Assert.Equal("Closed", ex.Extensions!["fromStatus"]);
        Assert.Equal("Open", ex.Extensions["toStatus"]);
    }
}
