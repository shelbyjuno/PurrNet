using UnityEngine;

namespace PurrNet.Examples
{
    public class AnimatedCharacterExample : NetworkIdentity
    {
        [SerializeField] NetworkAnimator _animator;
        
        void Update()
        {
            if (!isController) return;
            
            if (Input.GetKeyDown(KeyCode.Space))
            {
                _animator.SetTrigger("Jump");
            }
            
            _animator.SetBool("Running", Input.GetKey(KeyCode.LeftShift));
            _animator.SetFloat("RunSpeed", Mathf.PerlinNoise(Time.time, 0) * 2);
        }
    }
}
