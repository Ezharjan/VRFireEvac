using ch.sycoforge.Decal;
using ch.sycoforge.Decal.Projectors;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(EasyDecal))]
public class CandidateFilter : MonoBehaviour
{
    public GameObject ExclusiveReceiver;

    private EasyDecal decal;

    // Use this for initialization
    private void Start()
    {
        decal = GetComponent<EasyDecal>();

        var p = decal.Projector;

        if(p != null && p is BoxProjector)
        {
            BoxProjector bp = p as BoxProjector;
            bp.OnCandidatesProcessed += bp_OnCandidatesProcessed;
        }
    }

    void bp_OnCandidatesProcessed(List<Collider> colliders)
    {
        List<Collider> toRemove = new List<Collider>();

        foreach(Collider c in colliders)
        {
            if(!c.gameObject.Equals(ExclusiveReceiver))
            {
                toRemove.Add(c);
            }
        }

        foreach (Collider c in toRemove)
        {
            colliders.Remove(c);
        }
    }
}
