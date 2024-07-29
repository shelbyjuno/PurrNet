using System;
using PurrNet;
using UnityEngine;

public class Bullet : NetworkIdentity
{
    [SerializeField] private float speed = 8f;
    [SerializeField] private int damage = 25;
    [SerializeField] private float lifeTime = 2f;
    [SerializeField] private HitDetection hitDetection;

    private void Update()
    {
        transform.position += transform.forward * (speed * Time.deltaTime);
        lifeTime -= Time.deltaTime;

        if (lifeTime <= 0)
            Destroy(gameObject);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hitDetection == HitDetection.Owner && isOwner)
        {
            if (!other.TryGetComponent(out PlayerHealth health))
            {
                Destroy(gameObject);
                return;
            }

            if (!health.isOwner)
            {
                health.ChangeHealth(-damage);
                Destroy(gameObject);
            }
            
            return;
        } 
        
        if (hitDetection == HitDetection.Victim)
        {
            if (other.TryGetComponent(out PlayerHealth health) && health.isOwner && health.owner != owner)
            {
                health.ChangeHealth(-damage);
                Destroy(gameObject);
            }
            
            if(isOwner)
                Destroy(gameObject);
        }
    }

    [System.Serializable]
    private enum HitDetection
    {
        Owner,
        Victim
    }
}
