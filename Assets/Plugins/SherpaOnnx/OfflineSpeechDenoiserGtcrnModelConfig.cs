/// Copyright (c)  2025  Xiaomi Corporation (authors: Fangjun Kuang)

using System.Runtime.InteropServices;

namespace SherpaOnnx
{
    [StructLayout(LayoutKind.Sequential)]
    public struct OfflineSpeechDenoiserGtcrnModelConfig
    {
        [MarshalAs(UnmanagedType.LPStr)]
        public string Model;
    }
}
