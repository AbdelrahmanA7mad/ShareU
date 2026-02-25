using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace ShareU
{
    public class ShareHub : Hub
    {
        // This is a minimal Hub. We can add client-to-server methods here if needed,
        // but for now, we only need to broadcast from server to clients.
        
        // Clients will listen for "FilesUpdated"
    }
}
