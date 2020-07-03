﻿using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Provisioning.Client;
using Microsoft.Azure.Devices.Provisioning.Client.Transport;
using Microsoft.Azure.Devices.Shared;
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using XamTrack.Core.Helpers;

namespace XamTrack.Core.Services
{
    public class IoTDeviceClientService : IIoTDeviceClientService
    {
        private DeviceClient _deviceClient;
        private IAppConfigService _appConfigService;
        private IDeviceInfoService _deviceInfoService;
        private CancellationTokenSource _cancellationTokenSource;

        #region IIoTDeviceClientService
        public ConnectionStatus LastKnownConnectionStatus { get; set; }

        public ConnectionStatusChangeReason LastKnownConnectionChangeReason { get; set; }

        public string ConnectionStatus => LastKnownConnectionStatus.ToString();

        public event EventHandler<string> ConnectionStatusChanged;

        public IoTDeviceClientService(IAppConfigService appConfigService, IDeviceInfoService deviceInfoService)
        {
            _appConfigService = appConfigService;
            _deviceInfoService = deviceInfoService;

            _cancellationTokenSource = new CancellationTokenSource();

            LastKnownConnectionStatus = Microsoft.Azure.Devices.Client.ConnectionStatus.Disconnected;
        }

        private void ConnectionStatusChangesHandler(ConnectionStatus status, ConnectionStatusChangeReason reason)
        {
            LastKnownConnectionStatus = status;
            LastKnownConnectionChangeReason = reason;
            ConnectionStatusChanged?.Invoke(this, status.ToString());
        }

        public async Task<bool> Connect()
        {
            var deviceId = _deviceInfoService.GetDeviceId();

            if (string.IsNullOrEmpty(_appConfigService.AssignedEndPoint))
            {

                await Provision();
            }

            if (_deviceClient != null)
            {
                _cancellationTokenSource?.Cancel();
                await _deviceClient?.CloseAsync();
                _deviceClient.Dispose();
                _deviceClient = null;
            }

            var symetricKey = IoTHelper.GenerateSymmetricKey(deviceId, _appConfigService.DpsSymetricKey);

            var sasToken = IoTHelper.GenerateSasToken(_appConfigService.AssignedEndPoint, symetricKey, null);

            _deviceClient = DeviceClient.Create(_appConfigService.AssignedEndPoint,
                new DeviceAuthenticationWithToken(deviceId, sasToken),
                TransportType.Mqtt_WebSocket_Only);

            _deviceClient.SetConnectionStatusChangesHandler(ConnectionStatusChangesHandler);

            await _deviceClient.OpenAsync(_cancellationTokenSource.Token);

            // await _deviceClient.SetDesiredPropertyUpdateCallbackAsync(DesiredPropertyUpdateCallback, null, _iotHubCancellationTokenSource.Token);
            return true;
        }

        public async Task<bool> Disconnect()
        {
            await _deviceClient.CloseAsync();
            return true;
        }

        public Task<bool> InitialiseAsync(string IotHubEndpoint)
        {
            throw new NotImplementedException();
        }

        public async Task SendEventAsync(string message, CancellationToken cancellationToken)
        {
            if (LastKnownConnectionStatus == Microsoft.Azure.Devices.Client.ConnectionStatus.Connected)
            {
                var msg = new Message(Encoding.ASCII.GetBytes(message));

                await _deviceClient.SendEventAsync(msg);
            }
        }
        #endregion

        private async Task<bool> Provision()
        {
            var dpsGlobalEndpoint = _appConfigService.DpsGlobalEndpoint;
            var dpsIdScope = _appConfigService.DpsIdScope;
            var deviceId = _deviceInfoService.GetDeviceId();
            var dpsSymetricKey = IoTHelper.GenerateSymmetricKey(deviceId, _appConfigService.DpsSymetricKey);


            using (var security = new SecurityProviderSymmetricKey(deviceId, dpsSymetricKey, dpsSymetricKey))
            {
                using (var transport = new ProvisioningTransportHandlerHttp())
                {
                    var provisioningClient = ProvisioningDeviceClient.Create(dpsGlobalEndpoint, dpsIdScope, security, transport);

                    var regResult = await provisioningClient.RegisterAsync(_cancellationTokenSource.Token);

                    if (regResult.Status == ProvisioningRegistrationStatusType.Assigned)
                    {
                        _appConfigService.AssignedEndPoint = regResult.AssignedHub;
                    }
                    return true;
                }
            }
        }
    }
}
