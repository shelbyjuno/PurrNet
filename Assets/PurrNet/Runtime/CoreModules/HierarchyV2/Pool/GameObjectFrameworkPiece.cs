using System;
using System.Text;
using PurrNet.Pooling;

namespace PurrNet.Modules
{
    public struct GameObjectFrameworkPiece : IDisposable
    {
        public readonly PrefabPieceID pid;
        public readonly NetworkID id;
        public readonly int childCount;
        public DisposableList<int> inversedRelativePath;
        
        public GameObjectFrameworkPiece(PrefabPieceID pid, NetworkID id, int childCount, DisposableList<int> path)
        {
            this.pid = pid;
            this.id = id;
            this.childCount = childCount;
            this.inversedRelativePath = path;
        }

        public override string ToString()
        {
            StringBuilder builder = new();
            builder.Append("GameObjectFrameworkPiece: { ");
            builder.Append("Nid: ");
            builder.Append(id);
            builder.Append(", childCount: ");
            builder.Append(childCount);
            builder.Append(", Path: ");
            for (int i = 0; i < inversedRelativePath.Count; i++)
            {
                builder.Append(inversedRelativePath[i]);
                if (i < inversedRelativePath.Count - 1)
                    builder.Append(" <- ");
            }
            builder.Append(" }");
            return builder.ToString();
        }

        public void Dispose()
        {
            inversedRelativePath.Dispose();
        }
    }
}