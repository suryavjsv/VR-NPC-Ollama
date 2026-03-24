/// Copyright (c)  2024.5 by 东风破

using System.Runtime.InteropServices;

namespace SherpaOnnx
{
    [StructLayout(LayoutKind.Sequential)]
    public struct OfflineTtsVitsModelConfig
    {
        [MarshalAs(UnmanagedType.LPStr)]
        public string Model;

        [MarshalAs(UnmanagedType.LPStr)]
        public string Lexicon;

        [MarshalAs(UnmanagedType.LPStr)]
        public string Tokens;

        [MarshalAs(UnmanagedType.LPStr)]
        public string DataDir;

        public float NoiseScale;
        public float NoiseScaleW;
        public float LengthScale;

        [MarshalAs(UnmanagedType.LPStr)]
        public string DictDir;
    }
}