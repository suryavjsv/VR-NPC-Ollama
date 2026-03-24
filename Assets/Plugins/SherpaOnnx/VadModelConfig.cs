/// Copyright (c)  2024  Xiaomi Corporation (authors: Fangjun Kuang)

using System.Runtime.InteropServices;

namespace SherpaOnnx
{
    [StructLayout(LayoutKind.Sequential)]
    public struct VadModelConfig
    {
        public SileroVadModelConfig SileroVad;

        public int SampleRate;

        public int NumThreads;

        [MarshalAs(UnmanagedType.LPStr)]
        public string Provider;

        public int Debug;

        public TenVadModelConfig TenVad;
    }
}

