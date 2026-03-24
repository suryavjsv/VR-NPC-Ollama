/// Copyright (c)  2025  Xiaomi Corporation (authors: Fangjun Kuang)

using System.Runtime.InteropServices;

namespace SherpaOnnx
{
    [StructLayout(LayoutKind.Sequential)]
    public struct OfflineTtsZipVoiceModelConfig
    {
        [MarshalAs(UnmanagedType.LPStr)]
        public string Tokens;

        [MarshalAs(UnmanagedType.LPStr)]
        public string Encoder;

        [MarshalAs(UnmanagedType.LPStr)]
        public string Decoder;

        [MarshalAs(UnmanagedType.LPStr)]
        public string Vocoder;

        [MarshalAs(UnmanagedType.LPStr)]
        public string DataDir;

        [MarshalAs(UnmanagedType.LPStr)]
        public string Lexicon;

        public float FeatScale;
        public float Tshift;
        public float TargetRms;
        public float GuidanceScale;
    }
}
