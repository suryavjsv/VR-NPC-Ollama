/// Copyright (c)  2024  Xiaomi Corporation

using System.Runtime.InteropServices;

namespace SherpaOnnx
{

    [StructLayout(LayoutKind.Sequential)]
    public struct OfflineSpeakerDiarizationConfig
    {
        public OfflineSpeakerSegmentationModelConfig Segmentation;
        public SpeakerEmbeddingExtractorConfig Embedding;
        public FastClusteringConfig Clustering;

        public float MinDurationOn;
        public float MinDurationOff;
    }
}



