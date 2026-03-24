/// Copyright (c)  2024  Xiaomi Corporation

using System.Runtime.InteropServices;

namespace SherpaOnnx
{
    [StructLayout(LayoutKind.Sequential)]
    public struct KeywordSpotterConfig
    {
        public FeatureConfig FeatConfig;
        public OnlineModelConfig ModelConfig;

        public int MaxActivePaths;
        public int NumTrailingBlanks;
        public float KeywordsScore;
        public float KeywordsThreshold;

        [MarshalAs(UnmanagedType.LPStr)]
        public string KeywordsFile;

        [MarshalAs(UnmanagedType.LPStr)]
        public string KeywordsBuf;

        public int KeywordsBufSize;
    }
}
