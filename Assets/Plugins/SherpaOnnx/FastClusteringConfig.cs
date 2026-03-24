/// Copyright (c)  2024  Xiaomi Corporation

using System.Runtime.InteropServices;

namespace SherpaOnnx
{

    [StructLayout(LayoutKind.Sequential)]
    public struct FastClusteringConfig
    {
        public int NumClusters;
        public float Threshold;
    }
}
