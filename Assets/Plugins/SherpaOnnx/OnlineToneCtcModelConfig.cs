/// Copyright (c)  2025  Xiaomi Corporation (authors: Fangjun Kuang)

using System.Runtime.InteropServices;

namespace SherpaOnnx
{
    [StructLayout(LayoutKind.Sequential)]
    public struct OnlineToneCtcModelConfig
    {
        [MarshalAs(UnmanagedType.LPStr)]
        public string Model;
    }
}
