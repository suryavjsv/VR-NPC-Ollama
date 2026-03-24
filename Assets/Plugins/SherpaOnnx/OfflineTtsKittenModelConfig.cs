/// Copyright (c)  2025  Xiaomi Corporation (authors: Fangjun Kuang)

using System.Runtime.InteropServices;

namespace SherpaOnnx
{
    [StructLayout(LayoutKind.Sequential)]
    public struct OfflineTtsKittenModelConfig
    {
        [MarshalAs(UnmanagedType.LPStr)]
        public string Model;

        [MarshalAs(UnmanagedType.LPStr)]
        public string Voices;

        [MarshalAs(UnmanagedType.LPStr)]
        public string Tokens;

        [MarshalAs(UnmanagedType.LPStr)]
        public string DataDir;

        public float LengthScale;
    }
}
