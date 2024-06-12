using System;
using System.Collections;
using System.Collections.Generic;
using FishNet.Object;
using UnityEngine;

public class FindTheItem : NetworkBehaviour
{
    private bool i_Destroyed = false;

    private void OnTriggerEnter(Collider other)
    {
        if (!base.HasAuthority)
        {
            return;
        }
        
        if (i_Destroyed)
        {
            return;
        }

        if (other.GetComponent<PlayerSeeker>())
        {
            return;
        }
        
        i_Destroyed = true;
        PlayerSpawnManager.ObjectiveFoundItem();
    }
}
