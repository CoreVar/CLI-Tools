using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;

namespace CoreVar.CommandLineInterface.Distribution;

/// <summary>Windows trust, timestamp, exact publisher and signed application identity verification.</summary>
public sealed class WindowsNativeInstallerVerifier : INativeInstallerVerifier
{
    public void Verify(string path, NativeInstallerRelease release, NativeInstallerUpdatePolicy policy)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Authenticode verification requires Windows.");
        NativeInstallerUpdateClient.ValidateRelease(release, policy);
        var file = new TrustFile { Size = (uint)Marshal.SizeOf<TrustFile>(), Path = Path.GetFullPath(path) };
        var pointer = Marshal.AllocHGlobal(Marshal.SizeOf<TrustFile>());
        Marshal.StructureToPtr(file, pointer, false);
        var data = new TrustData { Size = (uint)Marshal.SizeOf<TrustData>(), UiChoice = 2, RevocationChecks = 1,
            UnionChoice = 1, File = pointer, StateAction = 1, ProviderFlags = 0x80 };
        var action = new Guid("00AAC56B-CD44-11D0-8CC2-00C04FC295EE");
        try
        {
            var result = WinVerifyTrust(new IntPtr(-1), ref action, ref data);
            if (result != 0) throw new InvalidDataException($"Windows rejected the installer signature (0x{result:X8}).");
            var provider = WTHelperProvDataFromStateData(data.StateData);
            if (provider == IntPtr.Zero) throw new InvalidDataException("Missing verified trust provider.");
            var signer = WTHelperGetProvSignerFromChain(provider, 0, false, 0);
            if (signer == IntPtr.Zero || WTHelperGetProvSignerFromChain(provider, 0, true, 0) == IntPtr.Zero)
                throw new InvalidDataException("A verified installer signer and timestamp are required.");
            var certificatePointer = WTHelperGetProvCertFromChain(signer, 0);
            if (certificatePointer == IntPtr.Zero) throw new InvalidDataException("Missing verified installer certificate.");
            var verified = Marshal.PtrToStructure<ProviderCertificate>(certificatePointer);
            if (verified.Certificate == IntPtr.Zero) throw new InvalidDataException("Missing certificate context.");
            var context = Marshal.PtrToStructure<CertificateContext>(verified.Certificate);
            if (context.Encoded == IntPtr.Zero || context.Length is 0 or > 65536) throw new InvalidDataException("Invalid verified certificate.");
            var bytes = new byte[context.Length]; Marshal.Copy(context.Encoded, bytes, 0, bytes.Length);
#if NET10_0_OR_GREATER
            using var certificate = X509CertificateLoader.LoadCertificate(bytes);
#else
            using var certificate = new X509Certificate2(bytes);
#endif
            if (!string.Equals(certificate.Subject, policy.PublisherSubject, StringComparison.Ordinal))
                throw new InvalidDataException("Installer publisher does not match the compiled trust policy.");
            var info = FileVersionInfo.GetVersionInfo(path);
            if (info.ProductName != policy.ProductName || info.CompanyName != policy.CompanyName ||
                !Version.TryParse(info.FileVersion, out var version) || version.Build < 0 ||
                new Version(version.Major, version.Minor, version.Build) != Version.Parse(release.Version))
                throw new InvalidDataException("Signed installer product, company or version differs from its release.");
        }
        finally
        {
            data.StateAction = 2; WinVerifyTrust(new IntPtr(-1), ref action, ref data);
            Marshal.DestroyStructure<TrustFile>(pointer); Marshal.FreeHGlobal(pointer);
        }
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct TrustFile { public uint Size; [MarshalAs(UnmanagedType.LPWStr)] public string Path; public IntPtr Handle; public IntPtr KnownSubject; }
    [StructLayout(LayoutKind.Sequential)]
    private struct TrustData { public uint Size; public IntPtr PolicyCallback; public IntPtr SipClient; public uint UiChoice;
        public uint RevocationChecks; public uint UnionChoice; public IntPtr File; public uint StateAction; public IntPtr StateData;
        public IntPtr UrlReference; public uint ProviderFlags; public uint UiContext; public IntPtr SignatureSettings; }
    [StructLayout(LayoutKind.Sequential)] private struct ProviderCertificate { public uint Size; public IntPtr Certificate; }
    [StructLayout(LayoutKind.Sequential)] private struct CertificateContext { public uint Encoding; public IntPtr Encoded; public uint Length; public IntPtr Info; public IntPtr Store; }
    [DllImport("wintrust.dll", ExactSpelling = true)] private static extern int WinVerifyTrust(IntPtr window, ref Guid action, ref TrustData data);
    [DllImport("wintrust.dll", ExactSpelling = true)] private static extern IntPtr WTHelperProvDataFromStateData(IntPtr state);
    [DllImport("wintrust.dll", ExactSpelling = true)] private static extern IntPtr WTHelperGetProvSignerFromChain(IntPtr provider, uint signer, [MarshalAs(UnmanagedType.Bool)] bool countersigner, uint countersignerIndex);
    [DllImport("wintrust.dll", ExactSpelling = true)] private static extern IntPtr WTHelperGetProvCertFromChain(IntPtr signer, uint certificate);
}
