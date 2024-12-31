using PurrNet.Packing;

namespace PurrNet.Modules
{
    public struct SpawnPacket : IPackedAuto
    {
        public SceneID sceneId;
        public SpawnID packetIdx;
        public GameObjectPrototype prototype;
        
        public override string ToString()
        {
            return $"SpawnPacket: {{ sceneId: {sceneId}, packetIdx: {packetIdx}, prototype: {prototype} }}";
        }
    }
}