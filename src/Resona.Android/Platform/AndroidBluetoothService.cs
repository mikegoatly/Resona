using System.Collections.Concurrent;

using Android.Bluetooth;
using Android.Content;

using Resona.Services.Bluetooth;

using Serilog;

using AndroidUtilLog = global::Android.Util.Log;

using AndroidBluetoothDevice = Android.Bluetooth.BluetoothDevice;
using BluetoothDevice = Resona.Services.Bluetooth.BluetoothDevice;

namespace Resona.Android.Platform;

/// <summary>
/// Classic Bluetooth (audio) on Android: discovery, bonding and A2DP connection state, mirroring the semantics of the
/// BlueZ implementation. "Known" = bonded devices plus anything discovered this session; "connected" = A2DP/headset
/// profile connected. Without permissions or with the adapter off it logs and returns empty results rather than throwing.
/// </summary>
internal sealed class AndroidBluetoothService : BluetoothServiceBase
{
    private const string ActionFound = "android.bluetooth.device.action.FOUND";
    private const string ActionNameChanged = "android.bluetooth.device.action.NAME_CHANGED";
    private const string A2dpStateAction = "android.bluetooth.a2dp.profile.action.CONNECTION_STATE_CHANGED";
    private const string HeadsetStateAction = "android.bluetooth.headset.profile.action.CONNECTION_STATE_CHANGED";
    private const string ProfileExtraState = "android.bluetooth.profile.extra.STATE";
    private const int ProfileConnected = 2;
    private static readonly TimeSpan BondTimeout = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(15);

    private readonly Context context;
    private readonly BluetoothAdapter? adapter;
    private readonly ConcurrentDictionary<string, BluetoothDevice> known = new();
    private readonly ConcurrentDictionary<string, TaskCompletionSource<Bond>> pendingBonds = new();
    private readonly HashSet<string> connected = [];
    private readonly object gate = new();
    private readonly CallbackReceiver receiver;
    private readonly TaskCompletionSource<IBluetoothProfile?> a2dpProxy = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool receiverRegistered;
    private bool started;
    private int scanning;

    public AndroidBluetoothService(Context context)
        : base(Serilog.Log.ForContext<AndroidBluetoothService>())
    {
        this.context = context.ApplicationContext ?? context;
        this.adapter = (this.context.GetSystemService(Context.BluetoothService) as BluetoothManager)?.Adapter;
        this.receiver = new CallbackReceiver(this.OnReceive);
    }

    public override async Task StartScanningAsync(CancellationToken cancellationToken)
    {
        if (!await this.PrepareAsync())
        {
            return;
        }

        try
        {
            this.LoadBondedDevices();
            if (this.adapter!.IsDiscovering)
            {
                this.SetScanning(true);
                return;
            }

            if (this.adapter.StartDiscovery())
            {
                this.SetScanning(true);
            }
            else
            {
                AndroidUtilLog.Warn(AndroidLog.Tag, "Bluetooth: StartDiscovery returned false");
            }
        }
        catch (Exception ex)
        {
            AndroidUtilLog.Error(AndroidLog.Tag, $"Bluetooth: start scanning failed: {ex}");
        }
    }

    public override Task StopScanningAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (this.adapter is not null && BluetoothPermissions.HasAll(this.context) && this.adapter.IsDiscovering)
            {
                this.adapter.CancelDiscovery();
            }
        }
        catch (Exception ex)
        {
            AndroidUtilLog.Error(AndroidLog.Tag, $"Bluetooth: stop scanning failed: {ex.Message}");
        }

        this.SetScanning(false);
        return Task.CompletedTask;
    }

    public override async Task<IReadOnlyList<BluetoothDevice>> GetKnownDevicesAsync(CancellationToken cancellationToken)
    {
        if (!await this.PrepareAsync())
        {
            return [];
        }

        try
        {
            this.LoadBondedDevices();
            await this.RefreshConnectedAsync();
        }
        catch (Exception ex)
        {
            AndroidUtilLog.Error(AndroidLog.Tag, $"Bluetooth: listing devices failed: {ex}");
        }

        return this.known.Values.OrderBy(d => d.Name).ToList();
    }

    public override async Task ConnectAsync(BluetoothDevice device, CancellationToken cancellationToken)
    {
        AndroidUtilLog.Info(AndroidLog.Tag, $"Bluetooth: connect requested for {device.Name} [{device.Address}]");
        if (!await this.PrepareAsync())
        {
            return;
        }

        try
        {
            var native = this.adapter!.GetRemoteDevice(device.Address)!;
            if (this.IsConnected(device.Address))
            {
                return;
            }

            device.Status = DeviceStatus.Connecting;

            if (native.BondState != Bond.Bonded && !await this.BondAsync(native, device, cancellationToken))
            {
                return;
            }

            await this.ConnectProfileAsync(native, device, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            AndroidUtilLog.Info(AndroidLog.Tag, "Bluetooth: connect cancelled");
        }
        catch (Exception ex)
        {
            AndroidUtilLog.Error(AndroidLog.Tag, $"Bluetooth: connect failed: {ex}");
        }
        finally
        {
            device.Status = this.IsConnected(device.Address) ? DeviceStatus.Connected : DeviceStatus.NotConnected;
            this.RefreshDevice(device.Address);
        }
    }

    public override Task ForgetDeviceAsync(BluetoothDevice device, CancellationToken cancellationToken)
    {
        try
        {
            if (this.adapter is not null && BluetoothPermissions.HasAll(this.context))
            {
                var native = this.adapter.GetRemoteDevice(device.Address)!;
                if (native.BondState == Bond.None)
                {
                    AndroidUtilLog.Info(AndroidLog.Tag, $"Bluetooth: {device.Address} is not bonded");
                }
                else
                {
                    // removeBond() is hidden API; there is no public way to unpair
                    var result = native.Class!.GetMethod("removeBond")!.Invoke(native);
                    AndroidUtilLog.Info(AndroidLog.Tag, $"Bluetooth: removeBond({device.Address}) -> {result}");
                }
            }
        }
        catch (Exception ex)
        {
            AndroidUtilLog.Error(AndroidLog.Tag, $"Bluetooth: could not remove bond (hidden API unavailable?): {ex.Message}");
        }

        this.RefreshDevice(device.Address);
        return Task.CompletedTask;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            try
            {
                if (this.receiverRegistered)
                {
                    this.context.UnregisterReceiver(this.receiver);
                    this.receiverRegistered = false;
                }

                if (this.a2dpProxy.Task.IsCompletedSuccessfully && this.a2dpProxy.Task.Result is { } proxy)
                {
                    this.adapter?.CloseProfileProxy(ProfileType.A2dp, proxy);
                }
            }
            catch (Exception ex)
            {
                AndroidUtilLog.Warn(AndroidLog.Tag, $"Bluetooth: dispose: {ex.Message}");
            }

            this.receiver.Dispose();
        }

        base.Dispose(disposing);
    }

    // ---------------------------------------------------------------------------------------------------------------
    // Setup
    // ---------------------------------------------------------------------------------------------------------------

    private async Task<bool> PrepareAsync()
    {
        if (this.adapter is null)
        {
            AndroidUtilLog.Warn(AndroidLog.Tag, "Bluetooth: no adapter on this device");
            return false;
        }

        BluetoothPermissions.GrantIfDeviceOwner(this.context);
        if (!BluetoothPermissions.HasAll(this.context))
        {
            AndroidUtilLog.Warn(AndroidLog.Tag, $"Bluetooth: missing permissions {string.Join(", ", BluetoothPermissions.Missing(this.context))}");
            return false;
        }

        if (!this.adapter.IsEnabled && !await this.TryEnableAsync())
        {
            AndroidUtilLog.Warn(AndroidLog.Tag, "Bluetooth: adapter is off and could not be enabled");
            return false;
        }

        this.Start();
        return true;
    }

    private async Task<bool> TryEnableAsync()
    {
        try
        {
            // Deprecated, and refused for ordinary apps on API 33+; try regardless (device owner may be allowed)
#pragma warning disable CA1422
            var requested = this.adapter!.Enable();
#pragma warning restore CA1422
            AndroidUtilLog.Info(AndroidLog.Tag, $"Bluetooth: Enable() returned {requested}");
            for (var i = 0; i < 20 && !this.adapter.IsEnabled; i++)
            {
                await Task.Delay(250);
            }
        }
        catch (Exception ex)
        {
            AndroidUtilLog.Warn(AndroidLog.Tag, $"Bluetooth: Enable() failed: {ex.Message}");
        }

        return this.adapter!.IsEnabled;
    }

    private void Start()
    {
        lock (this.gate)
        {
            if (this.started)
            {
                return;
            }

            this.started = true;
        }

        var filter = new IntentFilter();
        foreach (var action in new[]
        {
            ActionFound, ActionNameChanged, BluetoothAdapter.ActionDiscoveryStarted,
            BluetoothAdapter.ActionDiscoveryFinished, AndroidBluetoothDevice.ActionBondStateChanged,
            AndroidBluetoothDevice.ActionPairingRequest, AndroidBluetoothDevice.ActionAclConnected,
            AndroidBluetoothDevice.ActionAclDisconnected, A2dpStateAction, HeadsetStateAction,
        })
        {
            filter.AddAction(action);
        }

        if (OperatingSystem.IsAndroidVersionAtLeast(33))
        {
            this.context.RegisterReceiver(this.receiver, filter, ReceiverFlags.NotExported);
        }
        else
        {
            this.context.RegisterReceiver(this.receiver, filter);
        }

        this.receiverRegistered = true;

        // The A2DP proxy is how connected devices are queried; it binds asynchronously
        try
        {
            var listener = new ProfileListener(this.a2dpProxy);
            if (!this.adapter!.GetProfileProxy(this.context, listener, ProfileType.A2dp))
            {
                this.a2dpProxy.TrySetResult(null);
            }
        }
        catch (Exception ex)
        {
            AndroidUtilLog.Warn(AndroidLog.Tag, $"Bluetooth: A2DP proxy unavailable: {ex.Message}");
            this.a2dpProxy.TrySetResult(null);
        }
    }

    // ---------------------------------------------------------------------------------------------------------------
    // Connect
    // ---------------------------------------------------------------------------------------------------------------

    private async Task<bool> BondAsync(AndroidBluetoothDevice native, BluetoothDevice device, CancellationToken cancellationToken)
    {
        var address = device.Address;
        var tcs = new TaskCompletionSource<Bond>(TaskCreationOptions.RunContinuationsAsynchronously);
        this.pendingBonds[address] = tcs;
        try
        {
            if (this.adapter!.IsDiscovering)
            {
                // Discovery slows bonding down considerably
                this.adapter.CancelDiscovery();
            }

            AndroidUtilLog.Info(AndroidLog.Tag, $"Bluetooth: pairing with {device.Name}");
            if (native.BondState != Bond.Bonding && !native.CreateBond())
            {
                AndroidUtilLog.Warn(AndroidLog.Tag, "Bluetooth: CreateBond returned false");
                return false;
            }

            var finished = await Task.WhenAny(tcs.Task, Task.Delay(BondTimeout, cancellationToken));
            if (finished != tcs.Task)
            {
                cancellationToken.ThrowIfCancellationRequested();
                AndroidUtilLog.Warn(AndroidLog.Tag, "Bluetooth: pairing timed out");
                return false;
            }

            var bond = await tcs.Task;
            AndroidUtilLog.Info(AndroidLog.Tag, $"Bluetooth: pairing finished: {bond}");
            return bond == Bond.Bonded;
        }
        finally
        {
            this.pendingBonds.TryRemove(address, out _);
        }
    }

    private async Task ConnectProfileAsync(AndroidBluetoothDevice native, BluetoothDevice device, CancellationToken cancellationToken)
    {
        await this.RefreshConnectedAsync();
        if (this.IsConnected(device.Address))
        {
            return;
        }

        // A2DP connect() is hidden API (and privileged on newer Android), so it often fails; Android normally connects
        // audio devices by itself right after bonding, which the state broadcasts pick up.
        if (await this.a2dpProxy.Task is { } proxy)
        {
            try
            {
                var proxyObject = (Java.Lang.Object)proxy;
                var result = proxyObject.Class!.GetMethod("connect", Java.Lang.Class.FromType(typeof(AndroidBluetoothDevice)))!.Invoke(proxyObject, native);
                AndroidUtilLog.Info(AndroidLog.Tag, $"Bluetooth: A2DP connect() via reflection -> {result}");
            }
            catch (Exception ex)
            {
                AndroidUtilLog.Warn(AndroidLog.Tag, $"Bluetooth: A2DP connect() via reflection failed ({ex.Message}); waiting for automatic connection");
            }
        }

        var deadline = DateTime.UtcNow + ConnectTimeout;
        while (DateTime.UtcNow < deadline && !this.IsConnected(device.Address))
        {
            await Task.Delay(500, cancellationToken);
            await this.RefreshConnectedAsync();
        }

        if (!this.IsConnected(device.Address))
        {
            AndroidUtilLog.Warn(AndroidLog.Tag, $"Bluetooth: {device.Name} did not connect");
        }
    }

    // ---------------------------------------------------------------------------------------------------------------
    // State
    // ---------------------------------------------------------------------------------------------------------------

    private void LoadBondedDevices()
    {
        foreach (var native in this.adapter!.BondedDevices ?? [])
        {
            if (IsAudio(native))
            {
                this.Track(native);
            }
        }
    }

    private async Task RefreshConnectedAsync()
    {
        if (!this.a2dpProxy.Task.IsCompleted)
        {
            try
            {
                await this.a2dpProxy.Task.WaitAsync(TimeSpan.FromSeconds(3));
            }
            catch (TimeoutException)
            {
                return;
            }
        }

        if (await this.a2dpProxy.Task is not { } proxy)
        {
            return;
        }

        try
        {
            var current = (proxy.ConnectedDevices ?? []).Select(d => d.Address!).ToHashSet();
            foreach (var address in current)
            {
                this.SetConnected(address, true);
            }

            string[] stale;
            lock (this.gate)
            {
                stale = this.connected.Where(a => !current.Contains(a)).ToArray();
            }

            foreach (var address in stale)
            {
                this.SetConnected(address, false);
            }
        }
        catch (Exception ex)
        {
            AndroidUtilLog.Warn(AndroidLog.Tag, $"Bluetooth: querying connected devices failed: {ex.Message}");
        }
    }

    private BluetoothDevice Track(AndroidBluetoothDevice native)
    {
        var address = native.Address!;
        var name = BluetoothDeviceFilter.DisplayName(GetName(native), address);
        var paired = native.BondState == Bond.Bonded;
        var device = this.known.GetOrAdd(address, _ => new BluetoothDevice(name, address, this.IsConnected(address), paired, paired));
        device.Name = name;
        device.Paired = paired;
        device.Trusted = paired;
        return device;
    }

    private void RefreshDevice(string address)
    {
        if (this.adapter is null || !this.known.TryGetValue(address, out var device) || !BluetoothPermissions.HasAll(this.context))
        {
            return;
        }

        try
        {
            var native = this.adapter.GetRemoteDevice(address)!;
            device.Paired = native.BondState == Bond.Bonded;
            device.Trusted = device.Paired;
            device.Status = this.IsConnected(address) ? DeviceStatus.Connected : DeviceStatus.NotConnected;
        }
        catch (Exception ex)
        {
            AndroidUtilLog.Warn(AndroidLog.Tag, $"Bluetooth: refreshing {address} failed: {ex.Message}");
        }
    }

    private bool IsConnected(string address)
    {
        lock (this.gate)
        {
            return this.connected.Contains(address);
        }
    }

    private void SetConnected(string address, bool isConnected)
    {
        lock (this.gate)
        {
            if (isConnected ? !this.connected.Add(address) : !this.connected.Remove(address))
            {
                return;
            }

            this.known.TryGetValue(address, out var device);
            if (device is not null)
            {
                device.Status = isConnected ? DeviceStatus.Connected : DeviceStatus.NotConnected;
            }

            if (isConnected)
            {
                if (device is not null)
                {
                    this.OnDeviceConnected(device);
                }
            }
            else
            {
                this.OnDeviceDisconnected();
            }
        }
    }

    private void SetScanning(bool isScanning)
    {
        if (Interlocked.Exchange(ref this.scanning, isScanning ? 1 : 0) == (isScanning ? 1 : 0))
        {
            return;
        }

        lock (this.gate)
        {
            if (isScanning)
            {
                this.OnStartScanning();
            }
            else
            {
                this.OnStopScanning();
            }
        }
    }

    // ---------------------------------------------------------------------------------------------------------------
    // Broadcasts
    // ---------------------------------------------------------------------------------------------------------------

    private void OnReceive(Intent intent)
    {
        try
        {
            var action = intent.Action;
            var native = GetDevice(intent);
            if (action == BluetoothAdapter.ActionDiscoveryStarted)
            {
                this.SetScanning(true);
            }
            else if (action == BluetoothAdapter.ActionDiscoveryFinished)
            {
                this.SetScanning(false);
            }
            else if (native is null)
            {
                return;
            }
            else if (action == ActionFound || action == ActionNameChanged)
            {
                if (IsAudio(native))
                {
                    var isNew = !this.known.ContainsKey(native.Address!);
                    var device = this.Track(native);
                    if (isNew || action == ActionNameChanged)
                    {
                        lock (this.gate)
                        {
                            this.OnDeviceDiscovered(device);
                        }
                    }
                }
            }
            else if (action == AndroidBluetoothDevice.ActionBondStateChanged)
            {
                this.OnBondStateChanged(native, intent);
            }
            else if (action == AndroidBluetoothDevice.ActionPairingRequest)
            {
                // Legacy PIN pairing (older speakers); "Just Works" devices never get here
                if (intent.GetIntExtra(AndroidBluetoothDevice.ExtraPairingVariant, -1) == AndroidBluetoothDevice.PairingVariantPin)
                {
                    AndroidUtilLog.Info(AndroidLog.Tag, $"Bluetooth: PIN requested by {native.Address}; trying 0000");
                    #pragma warning disable CA1422
                    native.SetPin(System.Text.Encoding.ASCII.GetBytes("0000"));
#pragma warning restore CA1422
                }
            }
            else if (action == A2dpStateAction || action == HeadsetStateAction)
            {
                var state = intent.GetIntExtra(ProfileExtraState, -1);
                AndroidUtilLog.Info(AndroidLog.Tag, $"Bluetooth: profile state {state} for {native.Address}");
                this.SetConnected(native.Address!, state == ProfileConnected);
            }
            else if (action == AndroidBluetoothDevice.ActionAclConnected)
            {
                // Profile broadcasts are authoritative; ACL is only a fallback when there is no A2DP proxy
                if (this.a2dpProxy.Task.IsCompleted && this.a2dpProxy.Task.Result is null)
                {
                    this.SetConnected(native.Address!, true);
                }
            }
            else if (action == AndroidBluetoothDevice.ActionAclDisconnected)
            {
                this.SetConnected(native.Address!, false);
            }
        }
        catch (Exception ex)
        {
            AndroidUtilLog.Error(AndroidLog.Tag, $"Bluetooth: error handling {intent.Action}: {ex}");
        }
    }

    private void OnBondStateChanged(AndroidBluetoothDevice native, Intent intent)
    {
        var state = (Bond)intent.GetIntExtra(AndroidBluetoothDevice.ExtraBondState, (int)Bond.None);
        AndroidUtilLog.Info(AndroidLog.Tag, $"Bluetooth: bond state of {native.Address} is {state}");
        if (this.known.TryGetValue(native.Address!, out var device))
        {
            device.Paired = state == Bond.Bonded;
            device.Trusted = device.Paired;
        }

        if (state != Bond.Bonding && this.pendingBonds.TryGetValue(native.Address!, out var tcs))
        {
            tcs.TrySetResult(state);
        }
    }

    private static bool IsAudio(AndroidBluetoothDevice native)
    {
        try
        {
            var major = native.BluetoothClass?.MajorDeviceClass;
            return BluetoothDeviceFilter.IsAudioCandidate(major is null ? null : (int)major.Value);
        }
        catch (Exception)
        {
            return true;
        }
    }

    private static string? GetName(AndroidBluetoothDevice native)
        => OperatingSystem.IsAndroidVersionAtLeast(30) ? native.Alias ?? native.Name : native.Name;

    private static AndroidBluetoothDevice? GetDevice(Intent intent)
    {
        if (OperatingSystem.IsAndroidVersionAtLeast(33))
        {
            return intent.GetParcelableExtra(AndroidBluetoothDevice.ExtraDevice, Java.Lang.Class.FromType(typeof(AndroidBluetoothDevice))) as AndroidBluetoothDevice;
        }

#pragma warning disable CA1422
        return intent.GetParcelableExtra(AndroidBluetoothDevice.ExtraDevice) as AndroidBluetoothDevice;
#pragma warning restore CA1422
    }

    private sealed class CallbackReceiver(Action<Intent> callback) : BroadcastReceiver
    {
        public override void OnReceive(Context? context, Intent? intent)
        {
            if (intent is not null)
            {
                callback(intent);
            }
        }
    }

    private sealed class ProfileListener(TaskCompletionSource<IBluetoothProfile?> proxy) : Java.Lang.Object, IBluetoothProfileServiceListener
    {
        public void OnServiceConnected(ProfileType profile, IBluetoothProfile? bluetoothProfile)
            => proxy.TrySetResult(bluetoothProfile);

        public void OnServiceDisconnected(ProfileType profile)
        {
        }
    }
}
