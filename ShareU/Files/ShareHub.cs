using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace ShareU
{
    public class ShareHub : Hub
    {
        private readonly WebHostManager? _manager;

        public ShareHub(WebHostManager? manager = null)
        {
            _manager = manager;
        }

        // Host requests client to accept and start transfer
        public async Task SendTransferOffer(string itemName, long totalBytes, int fileCount, bool isFolder)
        {
            await Clients.Others.SendAsync("ReceiveTransferOffer", itemName, totalBytes, fileCount, isFolder);
        }

        // Client updates host with live transfer progress
        public async Task ReportProgress(string itemName, long bytesTransferred, long totalBytes, double speedMb, string currentFile)
        {
            _manager?.OnTransferProgress(new TransferProgressInfo
            {
                ItemName = itemName,
                BytesTransferred = bytesTransferred,
                TotalBytes = totalBytes,
                SpeedMb = speedMb,
                CurrentFile = currentFile
            });

            await Clients.Others.SendAsync("OnTransferProgress", itemName, bytesTransferred, totalBytes, speedMb, currentFile);
        }

        // Client notifies host when transfer is 100% complete
        public async Task ReportComplete(string itemName)
        {
            _manager?.OnTransferComplete(itemName);
            await Clients.Others.SendAsync("OnTransferComplete", itemName);
        }
    }
}
