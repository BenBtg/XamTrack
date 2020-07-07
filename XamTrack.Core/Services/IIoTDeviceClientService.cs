using Microsoft.Azure.Devices.Client;
using System;
using System.Threading.Tasks;

namespace XamTrack.Core.Services
{
    public interface IIoTDeviceClientService
    {        
        event EventHandler<ConnectionStatus> ConnectionStatusChanged;
        ConnectionStatus LastKnownConnectionStatus { get; }

        Task<bool> ConnectAsync();

        Task<bool> DisconnectAsync();

        Task SendEventAsync(string message);
    }
}
