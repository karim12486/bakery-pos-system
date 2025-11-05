namespace BakeryPOS.API.Core.Interfaces
{
    public interface INotificationService
    {
        Task SendNotificationAsync(string message);
    }
}