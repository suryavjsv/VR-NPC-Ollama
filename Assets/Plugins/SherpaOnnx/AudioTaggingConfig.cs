/// Copyright (c)  2025  Xiaomi Corporation (authors: Fangjun Kuang)

using System.Runtime.InteropServices;

namespace SherpaOnnx
{
    [StructLayout(LayoutKind.Sequential)]
    public struct AudioTaggingConfig
    {
        public AudioTaggingModelConfig Model;

        [MarshalAs(UnmanagedType.LPStr)]
        public string Labels;

        public int TopK;
    }
}
