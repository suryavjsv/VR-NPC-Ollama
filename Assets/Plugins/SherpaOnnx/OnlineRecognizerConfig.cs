/// Copyright (c)  2023  Xiaomi Corporation (authors: Fangjun Kuang)
/// Copyright (c)  2023 by manyeyes
/// Copyright (c)  2024.5 by 东风破

using System.Runtime.InteropServices;

namespace SherpaOnnx
{

    [StructLayout(LayoutKind.Sequential)]
    public struct OnlineRecognizerConfig
    {
        public FeatureConfig FeatConfig;
        public OnlineModelConfig ModelConfig;

        [MarshalAs(UnmanagedType.LPStr)]
        public string DecodingMethod;

        /// Used only when decoding_method is modified_beam_search
        /// Example value: 4
        public int MaxActivePaths;

        /// 0 to disable endpoint detection.
        /// A non-zero value to enable endpoint detection.
        public int EnableEndpoint;

        /// An endpoint is detected if trailing silence in seconds is larger than
        /// this value even if nothing has been decoded.
        /// Used only when enable_endpoint is not 0.
        public float Rule1MinTrailingSilence;

        /// An endpoint is detected if trailing silence in seconds is larger than
        /// this value after something that is not blank has been decoded.
        /// Used only when enable_endpoint is not 0.
        public float Rule2MinTrailingSilence;

        /// An endpoint is detected if the utterance in seconds is larger than
        /// this value.
        /// Used only when enable_endpoint is not 0.
        public float Rule3MinUtteranceLength;

        /// Path to the hotwords.
        [MarshalAs(UnmanagedType.LPStr)]
        public string HotwordsFile;

        /// Bonus score for each token in hotwords.
        public float HotwordsScore;

        public OnlineCtcFstDecoderConfig CtcFstDecoderConfig;

        [MarshalAs(UnmanagedType.LPStr)]
        public string RuleFsts;

        [MarshalAs(UnmanagedType.LPStr)]
        public string RuleFars;

        public float BlankPenalty;

        [MarshalAs(UnmanagedType.LPStr)]
        public string HotwordsBuf;

        public int HotwordsBufSize;

        public HomophoneReplacerConfig Hr;
    }
}
