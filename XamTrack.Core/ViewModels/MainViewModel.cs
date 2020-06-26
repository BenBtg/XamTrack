﻿using System;
using System.Timers;
using System.Threading.Tasks;
using TinyMvvm;
using TinyMvvm.IoC;
using Xamarin.Essentials;
using XamTrack.Core.Services;
using System.Windows.Input;
using Newtonsoft.Json;
using XamTrack.Core.Models;
using System.Diagnostics;

namespace XamTrack.Core.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        #region Properties
        private double _timerProgress;
        public double TimerProgress
        {
            get => _timerProgress;
            set => Set(ref _timerProgress, value);
        }

        private string _messageText;
        public string MessageText
        {
            get => _messageText;
            set => Set(ref _messageText, value);
        }

        private string _connectionStatus;
        public string ConnectionStatus
        {
            get => _connectionStatus;
            set => Set(ref _connectionStatus, value);
        }

        private string _deviceId;
        public string DeviceId
        {
            get => _deviceId;
            set => Set(ref _deviceId, value);
        }

        private string _country;
        public string Country
        {
            get => _country;
            set => Set(ref _country, value);
        }

        private string _city;
        public string City
        {
            get => _city;
            set => Set(ref _city, value);
        }

        private string _connected;
        public string Connected
        {
            get => _connected;
            set => Set(ref _connected, value);
        }

        private string _trackButtonText;
        public string TrackButtonText
        {
            get => _trackButtonText;
            set => Set(ref _trackButtonText, value);
        }

        private Location _currentLocation;
        public Location CurrentLocation
        {
            get => _currentLocation;
            set => Set(ref _currentLocation, value);
        }
        #endregion

        #region Commands
        private ICommand _connectCommand;
        public ICommand ConnectCommand => _connectCommand = new TinyCommand(async () =>
        {
            ConnectionStatus = "Connecting";
            await _ioTDeviceClientService.Connect();
            TrackButtonText = "Stop Tracking";
            TimerProgress = 1.0;
            _messageTimer.Start();
            
        });

        private ICommand? _disconnect;
        public ICommand Disconnect => _disconnect ??= new TinyCommand(async () =>
        {
            await _ioTDeviceClientService.Disconnect();
        });

        #endregion

        private bool _isTracking;
        private IGeolocationService _geolocationService;
        private IDeviceInfoService _deviceInfoService;
        private IIoTDeviceClientService _ioTDeviceClientService;

        Timer _messageTimer;

        readonly int MessageTimerPeriod = 5000;

        System.Threading.CancellationToken _cancellationToken;

        public MainViewModel(IGeolocationService geolocationService, IIoTDeviceClientService ioTDeviceClientService, IDeviceInfoService deviceInfoService)
        {
            _cancellationToken = new System.Threading.CancellationToken();
            _geolocationService = geolocationService;
            _deviceInfoService = deviceInfoService;
            _ioTDeviceClientService = ioTDeviceClientService;
            _ioTDeviceClientService.ConnectionStatusChanged += _ioTDeviceClientService_ConnectionStatusChanged;
            TrackButtonText = "Start Tracking";
            ConnectionStatus = "Disconnected";
        }

        private void _ioTDeviceClientService_ConnectionStatusChanged(object sender, string e)
        {
            ConnectionStatus = e;
        }

        public async override Task Initialize()
        {
            await base.Initialize();
            DeviceId = _deviceInfoService.GetDeviceId();
            
            CurrentLocation = await _geolocationService?.GetLastKnownLocationAsync();
            City = await _geolocationService?.GetCityName(CurrentLocation);

            _messageTimer = new Timer(MessageTimerPeriod);
            _messageTimer.Elapsed += _timer_ElapsedAsync;
            _messageTimer.AutoReset = true;            
        }

        private async void _timer_ElapsedAsync(object sender, ElapsedEventArgs e)
        {
            Debug.WriteLine("MessageTimerElapsed");

            await MainThread.InvokeOnMainThreadAsync(() => TimerProgress = 0);
            await UpdateCurrentLocationAsync();

            var message = new IoTMessage(CurrentLocation, MessageText,
                _deviceInfoService.GetDeviceId(),
                _deviceInfoService.GetDeviceName(),
                _deviceInfoService.GetDeviceModel()
                );
            var messagejson = JsonConvert.SerializeObject(message);

            await _ioTDeviceClientService.SendEventAsync(messagejson, _cancellationToken);
            await MainThread.InvokeOnMainThreadAsync(()=> TimerProgress = 1.0);
        }

        private async Task UpdateCurrentLocationAsync()
        {
            CurrentLocation = await _geolocationService.GetLocationAsync();
            City = await _geolocationService?.GetCityName(CurrentLocation);
        }
    }
}