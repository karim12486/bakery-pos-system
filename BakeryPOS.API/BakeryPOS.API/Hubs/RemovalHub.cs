using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace BakeryPOS.API.Hubs
{
    public class RemovalHub : Hub
    {
        // This method will be called by an admin client to join a specific "Admins" group.
        // This allows us to send messages to all connected admins at once.
        public async Task JoinAdminsGroup()
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, "Admins");
        }

        // This is a server-side method that the frontend doesn't call.
        // Our API controller will call this method to notify all admins.
        public async Task NotifyAdminsOfNewRequest(object request)
        {
            // This sends a message named "NewRemovalRequest" to all clients in the "Admins" group.
            // The 'request' object will be the details of the removal request.
            await Clients.Group("Admins").SendAsync("NewRemovalRequest", request);
        }

        // This is another server-side method our API will call.
        public async Task NotifyCashierOfStatusUpdate(string cashierConnectionId, object update)
        {
            // This sends a message to one specific cashier client, identified by their unique connection ID.
            await Clients.Client(cashierConnectionId).SendAsync("RemovalRequestStatusChanged", update);
        }
    }
}