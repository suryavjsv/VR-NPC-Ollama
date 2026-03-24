/// Copyright (c)  2025  Xiaomi Corporation (authors: Fangjun Kuang)

using System.Runtime.InteropServices;

namespace SherpaOnnx
{
    [StructLayout(LayoutKind.Sequential)]
    public struct OfflineSpeechDenoiserModelConfig
    {
        public OfflineSpeechDenoiserGtcrnModelConfig Gtcrn;

        public int NumThreads;

        public int Debug;

        [MarshalAs(UnmanagedType.LPStr)]
        public string Provider;

        public OfflineSpeechDenoiserDpdfNetModelConfig Dpdfnet;
    }
}
