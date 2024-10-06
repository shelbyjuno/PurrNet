using UnityEngine;

public class NonNetworkedAnimationController : MonoBehaviour
{
    private static readonly int Running = Animator.StringToHash("Running");
    private static readonly int RunSpeed = Animator.StringToHash("RunSpeed");
    
    [SerializeField] Animator _animator;
    [SerializeField] bool _isRunning;
    [SerializeField] float _runningSpeed;

    private void Update()
    {
        _animator.SetBool(Running, _isRunning);
        _animator.SetFloat(RunSpeed, _runningSpeed);
    }
}
