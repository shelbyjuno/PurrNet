using UnityEngine;

namespace PurrNet.Examples
{
    public class AnimatedCharacterExample : NetworkIdentity
    {
        [SerializeField] NetworkAnimator _animator;
        
        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                _animator.SetTrigger("Jump");
            }
            
            if (Input.GetKeyDown(KeyCode.LeftShift))
            {
                _animator.SetBool("Running", true);
            }
            
            if (Input.GetKeyUp(KeyCode.LeftShift))
            {
                _animator.SetBool("Running", false);
            }
            
            if (Input.GetKey(KeyCode.LeftShift))
                _animator.SetFloat("RunSpeed", Mathf.PerlinNoise(Time.time, 0) * 2);
        }
    }
}
