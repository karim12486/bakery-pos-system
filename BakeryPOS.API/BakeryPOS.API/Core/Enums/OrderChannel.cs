namespace BakeryPOS.API.Core.Enums;

/// <summary>
/// How the order is fulfilled. Phase A defaults every order to <see cref="Takeaway"/>
/// (counter-service: customer orders at the counter and takes the goods). Phase B adds
/// <see cref="DineIn"/> (eaten at a table) and <see cref="Delivery"/> (handed to a
/// delivery driver / 3rd-party platform).
/// </summary>
public enum OrderChannel
{
    DineIn,
    Takeaway,
    Delivery
}
