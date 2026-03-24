/// Copyright (c)  2024.5 by 东风破

using System.Runtime.InteropServices;

namespace SherpaOnnx
{
    [StructLayout(LayoutKind.Sequential)]
    public struct OfflineTtsModelConfig
    {
        public OfflineTtsVitsModelConfig Vits;
        public int NumThreads;
        public int Debug;

        [MarshalAs(UnmanagedType.LPStr)]
        public string Provider;

        public OfflineTtsMatchaModelConfig Matcha;
        public OfflineTtsKokoroModelConfig Kokoro;
        public OfflineTtsKittenModelConfig Kitten;
        public OfflineTtsZipVoiceModelConfig ZipVoice;
        public OfflineTtsPocketModelConfig Pocket;
        public OfflineTtsSupertonicModelConfig Supertonic;
    }
}
