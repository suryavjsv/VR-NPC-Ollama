/// Copyright (c)  2026  Xiaomi Corporation

using System.Runtime.InteropServices;

namespace SherpaOnnx
{
    [StructLayout(LayoutKind.Sequential)]
    public struct OfflineSpeechDenoiserDpdfNetModelConfig
    {
        [MarshalAs(UnmanagedType.LPStr)]
        public string Model;
    }
}
