using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace QuickKill;

internal static class AcrylicHelper
{
    private enum AccentState { Disabled = 0, BlurBehind = 3, AcrylicBlurBehind = 4 }

    [StructLayout(LayoutKind.Sequential)]
    private struct AccentPolicy
    {
        public AccentState AccentState;
        public uint AccentFlags;
        public uint GradientColor; // 0xAABBGGRR
        public uint AnimationId;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowCompositionAttribData
    {
        public int Attribute;
        public IntPtr Data;
        public int SizeOfData;
    }

    [DllImport("user32.dll")]
    private static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttribData data);

    // Dark smoked glass #202024 at ~72% — AABBGGRR = 0xB8242020
    public static void Apply(Window window)
    {
        try
        {
            var accent = new AccentPolicy
            {
                AccentState   = AccentState.AcrylicBlurBehind,
                AccentFlags   = 0x20,
                GradientColor = 0xB8242020,
            };
            var size = Marshal.SizeOf(accent);
            var ptr  = Marshal.AllocHGlobal(size);
            Marshal.StructureToPtr(accent, ptr, false);
            var data = new WindowCompositionAttribData { Attribute = 19, SizeOfData = size, Data = ptr };
            SetWindowCompositionAttribute(new WindowInteropHelper(window).Handle, ref data);
            Marshal.FreeHGlobal(ptr);
        }
        catch { }
    }
}
