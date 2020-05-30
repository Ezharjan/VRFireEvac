using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class smoke_fire_reduce : MonoBehaviour
{

    void Start()
    {
        this.GetComponent<ParticleSystem>().Stop();
    }


    void Update()
    {
        
    }

    private void OnParticleCollision(GameObject other)
    {
   
        GameObject fire = other.gameObject;
        firelife firelife = fire.GetComponent<firelife>();
        if (firelife != null)
        {
            Debug.Log("collising");
            firelife.Reduce();
        }

    }
}
