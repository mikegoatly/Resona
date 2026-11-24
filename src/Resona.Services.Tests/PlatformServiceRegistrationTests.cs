using Microsoft.Extensions.DependencyInjection;

using PiJuiceSharp;

using Resona.Services.Audio;
using Resona.Services.Battery;
using Resona.Services.Bluetooth;
using Resona.Services.OS;

namespace Resona.Services.Tests;

public sealed class PlatformServiceRegistrationTests
{
    /// <summary>Every interface a head must provide; the shared services depend on (or the UI resolves) each of them.</summary>
    public static TheoryData<Type> RequiredPlatformInterfaces => new()
    {
        typeof(IBluetoothService),
        typeof(IAudioOutputService),
        typeof(ILogService),
        typeof(IBatteryStateProvider),
        typeof(IOsCommandExecutor),
    };

    [Theory]
    [MemberData(nameof(RequiredPlatformInterfaces))]
    public void AddDevPlatformServices_RegistersEveryPlatformInterface(Type serviceType)
    {
        var services = new ServiceCollection().AddDevPlatformServices();

        Assert.Single(services, d => d.ServiceType == serviceType);
    }

    [Fact]
    public void AddDevPlatformServices_BatteryAndOsCommands_AreConstructibleWithoutHardware()
    {
        using var provider = new ServiceCollection().AddDevPlatformServices().BuildServiceProvider();

        Assert.IsType<FakeBatteryStateProvider>(provider.GetRequiredService<IBatteryStateProvider>());
        Assert.IsType<DevOsCommandExecutor>(provider.GetRequiredService<IOsCommandExecutor>());
    }

    [Theory]
    [MemberData(nameof(RequiredPlatformInterfaces))]
    public void AddLinuxPlatformServices_RegistersEveryPlatformInterface(Type serviceType)
    {
        // PiJuiceStatus.Create() talks to I2C while registering, so only the Pi (Linux) can build this set
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Linux platform services need Pi hardware to register");

        var services = new ServiceCollection().AddLinuxPlatformServices();

        Assert.Single(services, d => d.ServiceType == serviceType);
        Assert.Single(services, d => d.ServiceType == typeof(IPiJuiceStatus));
    }
}
