using PurrNet;
using PurrNet.Logging;

public class SomeBehaviour : NetworkBehaviour
{
    protected override void OnDestroy()
    {
        PurrLogger.Log("SomeBehaviour OnDestroy");
    }
    
    public override void OnEnable()
    {
    }

    public static void OnDisable()
    {
    }
}