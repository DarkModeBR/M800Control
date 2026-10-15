using System.Runtime.InteropServices;
using M800.Core.Protocol;
using static M800.Core.Hid.NativeMethods;

namespace M800.Core.Hid;

public sealed record HidCollectionInfo(string Path, ushort ProductId, ushort UsagePage, ushort Usage, int InputLength, int FeatureLength);

/// <summary>Localiza as coleções HID do M800 como o driver oficial: feature de 33 bytes = configuração,
/// input de 33 bytes (usage page 0x0001) = notificações. Prefere o cabo ao receptor.</summary>
public sealed class HidMouseTransportFactory : IMouseTransportFactory
{
    public IMouseTransport? TryOpen()
    {
        var collections = Enumerate();
        foreach (var pid in new[] { ProtocolConstants.ProductIdWired, ProtocolConstants.ProductIdDongle })
        {
            var config = collections.FirstOrDefault(c => c.ProductId == pid && c.FeatureLength == ProtocolConstants.ReportLength);
            if (config is null)
                continue;
            var notify = collections.FirstOrDefault(c => c.ProductId == pid && c.UsagePage == 0x0001
                && c.InputLength == ProtocolConstants.ReportLength);
            try
            {
                return new HidMouseTransport(pid, config.Path, notify?.Path);
            }
            catch (IOException)
            {
                // coleção ocupada ou removida: tenta a próxima
            }
        }
        return null;
    }

    public static List<HidCollectionInfo> Enumerate()
    {
        var result = new List<HidCollectionInfo>();
        HidD_GetHidGuid(out var hidGuid);
        IntPtr set = SetupDiGetClassDevs(ref hidGuid, IntPtr.Zero, IntPtr.Zero, DIGCF_PRESENT | DIGCF_DEVICEINTERFACE);
        if (set == IntPtr.Zero || set == new IntPtr(-1))
            return result;
        try
        {
            var data = new SP_DEVICE_INTERFACE_DATA { cbSize = Marshal.SizeOf<SP_DEVICE_INTERFACE_DATA>() };
            for (int index = 0; SetupDiEnumDeviceInterfaces(set, IntPtr.Zero, ref hidGuid, index, ref data); index++)
            {
                var path = GetPath(set, ref data);
                if (path is not null && Describe(path) is { } info)
                    result.Add(info);
            }
        }
        finally
        {
            SetupDiDestroyDeviceInfoList(set);
        }
        return result;
    }

    private static string? GetPath(IntPtr set, ref SP_DEVICE_INTERFACE_DATA data)
    {
        SetupDiGetDeviceInterfaceDetail(set, ref data, IntPtr.Zero, 0, out int size, IntPtr.Zero);
        if (size <= 0)
            return null;
        IntPtr detail = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.WriteInt32(detail, IntPtr.Size == 8 ? 8 : 6);
            if (!SetupDiGetDeviceInterfaceDetail(set, ref data, detail, size, out _, IntPtr.Zero))
                return null;
            return Marshal.PtrToStringUni(detail + 4);
        }
        finally
        {
            Marshal.FreeHGlobal(detail);
        }
    }

    private static HidCollectionInfo? Describe(string path)
    {
        using var handle = CreateFile(path, 0, FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
        if (handle.IsInvalid)
            return null;
        var attributes = new HIDD_ATTRIBUTES { Size = Marshal.SizeOf<HIDD_ATTRIBUTES>() };
        if (!HidD_GetAttributes(handle, ref attributes) || attributes.VendorID != ProtocolConstants.VendorId)
            return null;
        if (!HidD_GetPreparsedData(handle, out IntPtr preparsed))
            return null;
        try
        {
            var caps = new HIDP_CAPS { Reserved = new ushort[17] };
            HidP_GetCaps(preparsed, ref caps);
            return new HidCollectionInfo(path, attributes.ProductID, caps.UsagePage, caps.Usage,
                caps.InputReportByteLength, caps.FeatureReportByteLength);
        }
        finally
        {
            HidD_FreePreparsedData(preparsed);
        }
    }
}
