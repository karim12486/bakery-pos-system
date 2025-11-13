public interface INotificationService
{
    // Old method: Task SendNotificationAsync(string message);
    Task SendNotificationAsync(string caption, string? filePath = null); // New method
}