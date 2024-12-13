using System;
using System.Text;
using PurrNet.Pooling;

namespace PurrNet.Modules
{
    public struct GameObjectPrototype : IDisposable
    {
        public DisposableList<GameObjectFrameworkPiece> framework;

        public void Dispose()
        {
            for (var i = 0; i < framework.Count; i++)
            {
                var piece = framework[i];
                piece.Dispose();
            }

            framework.Dispose();
        }

        public override string ToString()
        {
            StringBuilder builder = new();
            builder.Append("GameObjectPrototype: {\n    ");
            for (int i = 0; i < framework.Count; i++)
            {
                builder.Append(framework[i]);
                if (i < framework.Count - 1)
                    builder.Append("\n    ");
            }
            builder.Append("\n}");
            return builder.ToString();
        }
    }
}