/// Copyright (c)  2024.5 by 东风破

using System.Runtime.InteropServices;

namespace SherpaOnnx
{
    [StructLayout(LayoutKind.Sequential)]
    public struct SpeakerEmbeddingExtractorConfig
    {
        [MarshalAs(UnmanagedType.LPStr)]
        public string Model;

        public int NumThreads;
        public int Debug;

        [MarshalAs(UnmanagedType.LPStr)]
        public string Provider;
    }

}