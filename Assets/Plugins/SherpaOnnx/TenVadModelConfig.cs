/// Copyright (c)  2025  Xiaomi Corporation (authors: Fangjun Kuang)

using System.Runtime.InteropServices;

namespace SherpaOnnx
{
    [StructLayout(LayoutKind.Sequential)]
    public struct TenVadModelConfig
    {
        [MarshalAs(UnmanagedType.LPStr)]
        public string Model;

        public float Threshold;

        public float MinSilenceDuration;

        public float MinSpeechDuration;

        public int WindowSize;

        public float MaxSpeechDuration;
    }
}
