/// Copyright (c)  2026  Xiaomi Corporation (authors: Fangjun Kuang)

using System.Runtime.InteropServices;

namespace SherpaOnnx
{
    [StructLayout(LayoutKind.Sequential)]
    public struct OnlinePunctuationModelConfig
    {
        [MarshalAs(UnmanagedType.LPStr)]
        public string CnnBiLstm;

        [MarshalAs(UnmanagedType.LPStr)]
        public string BpeVocab;

        public int NumThreads;

        public int Debug;

        [MarshalAs(UnmanagedType.LPStr)]
        public string Provider;
    }
}
