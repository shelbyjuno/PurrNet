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
        public readonly bool isActive;
        public DisposableList<int> inversedRelativePath;
        public DisposableList<bool> enabled;
        
        public GameObjectFrameworkPiece(PrefabPieceID pid, NetworkID id, int childCount, bool isActive,
            DisposableList<int> path, DisposableList<bool> enabled)
        {
            this.pid = pid;
            this.id = id;
            this.childCount = childCount;
            this.inversedRelativePath = path;
            this.enabled = enabled;
            this.isActive = isActive;
        }

        public override string ToString()
        {
            StringBuilder builder = new();
            builder.Append("GameObjectFrameworkPiece: { ");
            builder.Append("Pid: ");
            builder.Append(pid);
            builder.Append(", Nid: ");
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
            builder.Append(", Enabled: ");
            for (int i = 0; i < enabled.Count; i++)
            {
                builder.Append(enabled[i]);
                if (i < enabled.Count - 1)
                    builder.Append(", ");
            }
            builder.Append(" }");
            return builder.ToString();
        }

        public void Dispose()
        {
            inversedRelativePath.Dispose();
            enabled.Dispose();
        }
    }
}