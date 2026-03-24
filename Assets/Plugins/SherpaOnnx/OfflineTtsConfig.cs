/// Copyright (c)  2024.5 by 东风破

using System.Runtime.InteropServices;

namespace SherpaOnnx
{
    [StructLayout(LayoutKind.Sequential)]
    public struct OfflineTtsConfig
    {
        public OfflineTtsModelConfig Model;

        [MarshalAs(UnmanagedType.LPStr)]
        public string RuleFsts;

        public int MaxNumSentences;

        [MarshalAs(UnmanagedType.LPStr)]
        public string RuleFars;

        public float SilenceScale;
    }
}
