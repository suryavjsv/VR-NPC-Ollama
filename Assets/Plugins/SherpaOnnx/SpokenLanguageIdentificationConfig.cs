/// Copyright (c)  2024.5 by 东风破

using System.Runtime.InteropServices;

namespace SherpaOnnx
{
    public struct SpokenLanguageIdentificationConfig
    {
        public SpokenLanguageIdentificationWhisperConfig Whisper;

        public int NumThreads;
        public int Debug;

        [MarshalAs(UnmanagedType.LPStr)]
        public string Provider;
    }

}