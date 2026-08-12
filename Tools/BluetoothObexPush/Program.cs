using InTheHand.Net;
using InTheHand.Net.Bluetooth;
using InTheHand.Net.Sockets;

static int Fail(string message, int code = 1)
{
    Console.Error.WriteLine(message);
    return code;
}

if (args.Length < 2)
{
    return Fail(
        "Usage: BluetoothObexPush <apkPath> <deviceName> [<deviceName2> ...]\n" +
        "Example: BluetoothObexPush app.apk \"TECNO POVA 7 Ultra 5G\" \"TECNO CAMON 20 Pro\"");
}

var apkPath = Path.GetFullPath(args[0]);
var deviceNames = args.Skip(1)
    .Select(a => a.Trim())
    .Where(a => !string.IsNullOrWhiteSpace(a))
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToArray();

if (!File.Exists(apkPath))
    return Fail($"APK not found: {apkPath}");

if (deviceNames.Length == 0)
    return Fail("Device name is empty.");

var radio = BluetoothRadio.Default;
if (radio is null)
    return Fail("No Bluetooth radio found. Enable Bluetooth on this PC.");

if (radio.Mode == RadioMode.PowerOff)
{
    Console.WriteLine("Bluetooth radio is off; switching to Connectable...");
    try
    {
        radio.Mode = RadioMode.Connectable;
    }
    catch (Exception ex)
    {
        return Fail($"Could not enable Bluetooth radio: {ex.Message}");
    }
}

Console.WriteLine($"Radio: {radio.Name} ({radio.LocalAddress}), mode={radio.Mode}");
Console.WriteLine($"Looking for device among: {string.Join(" | ", deviceNames.Select(n => $"\"{n}\""))}");

bool NameMatches(BluetoothDeviceInfo d) =>
    deviceNames.Any(n => string.Equals(d.DeviceName, n, StringComparison.OrdinalIgnoreCase));

BluetoothDeviceInfo? device;
using (var client = new BluetoothClient())
{
    device = client.PairedDevices.FirstOrDefault(NameMatches);

    if (device is null)
    {
        Console.WriteLine("Not in paired list; discovering nearby devices...");
        var discovered = client.DiscoverDevices(30);
        device = discovered.FirstOrDefault(NameMatches);
    }
}

if (device is null)
{
    return Fail(
        $"None of the target devices found among paired/discovered devices:\n  {string.Join("\n  ", deviceNames)}\n" +
        "Pair the phone in Windows Settings → Bluetooth, keep it nearby, then retry.");
}

device.Refresh();
Console.WriteLine(
    $"Found: {device.DeviceName} [{device.DeviceAddress}] authenticated={device.Authenticated}");

if (!device.Authenticated)
{
    Console.WriteLine("Warning: device is not authenticated/paired; OBEX may fail.");
}

var remoteName = Path.GetFileName(apkPath);
var addrStr = device.DeviceAddress.ToString("N");
var uri = new Uri($"obex://{addrStr}/{remoteName}");

Console.WriteLine($"OBEX push: {apkPath}");
Console.WriteLine($"  → {uri}");
Console.WriteLine("Accept the file on the phone if prompted.");

try
{
    var request = new ObexWebRequest(uri)
    {
        Timeout = 10 * 60 * 1000 // large APK over BT can be slow
    };
    request.ReadFile(apkPath);

    using var response = (ObexWebResponse)request.GetResponse();
    Console.WriteLine($"OBEX status: {response.StatusCode} (0x{(int)response.StatusCode:X})");

    // Status often includes Final (0x80); success family is 0x20–0x2F.
    var code = (int)(response.StatusCode & ~ObexStatusCode.Final);
    if (code is >= 0x20 and <= 0x2F)
    {
        Console.WriteLine("OK: file sent via Bluetooth OBEX.");
        return 0;
    }

    return Fail($"OBEX push failed with status {response.StatusCode}.");
}
catch (Exception ex)
{
    return Fail($"OBEX push error: {ex.GetType().Name}: {ex.Message}");
}
