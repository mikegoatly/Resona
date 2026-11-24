using Resona.Services.Bluetooth;

namespace Resona.Services.Tests;

public sealed class BluetoothDeviceFilterTests
{
    [Theory]
    [InlineData(BluetoothDeviceFilter.AudioVideoMajorClass, true)]
    [InlineData(null, true)]
    [InlineData(0x0100, false)] // computer
    [InlineData(0x0200, false)] // phone
    [InlineData(0x0500, false)] // peripheral
    public void IsAudioCandidate_ByMajorClass(int? majorClass, bool expected)
        => Assert.Equal(expected, BluetoothDeviceFilter.IsAudioCandidate(majorClass));

    [Theory]
    [InlineData("Sony WH-1000XM4", "AA:BB:CC:DD:EE:FF", "Sony WH-1000XM4")]
    [InlineData(null, "AA:BB:CC:DD:EE:FF", "AA:BB:CC:DD:EE:FF")]
    [InlineData("", "AA:BB:CC:DD:EE:FF", "AA:BB:CC:DD:EE:FF")]
    [InlineData("  ", "AA:BB:CC:DD:EE:FF", "AA:BB:CC:DD:EE:FF")]
    public void DisplayName_FallsBackToAddress(string? name, string address, string expected)
        => Assert.Equal(expected, BluetoothDeviceFilter.DisplayName(name, address));
}
