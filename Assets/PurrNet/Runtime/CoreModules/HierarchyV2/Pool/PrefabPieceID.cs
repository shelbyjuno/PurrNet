using System;

namespace PurrNet.Modules
{
    public readonly struct PrefabPieceID : IEquatable<PrefabPieceID>
    {
        public readonly int prefabId;
        public readonly int depthIndex;
        public readonly int siblingIndex;

        public PrefabPieceID(int prefabId, int depthId, int siblingId)
        {
            this.prefabId = prefabId;
            depthIndex = depthId;
            siblingIndex = siblingId;
        }

        public bool Equals(PrefabPieceID other)
        {
            return prefabId == other.prefabId && depthIndex == other.depthIndex && siblingIndex == other.siblingIndex;
        }

        public override bool Equals(object obj)
        {
            return obj is PrefabPieceID other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(prefabId, depthIndex, siblingIndex);
        }
    }
}