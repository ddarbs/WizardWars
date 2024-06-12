using System;
using System.Collections;
using System.Collections.Generic;
using FishNet.Object;
using UnityEngine;
using Random = UnityEngine.Random;

public class Lightning : NetworkBehaviour
{
    private LayerMask i_PropLayerMask;
    [SerializeField] private AudioSource i_Audio;
    [SerializeField] private GameObject i_LightningVisuals;
    [SerializeField] private AudioClip i_LightningSound;
    private void Awake()
    {
        i_PropLayerMask = LayerMask.GetMask("Prop", "PlayerProp");
    }

    public override void OnStartServer()
    {
        base.OnStartServer();

        StartCoroutine(ServerLightningDelay());
    }

    private IEnumerator ServerLightningDelay()
    {
        yield return new WaitForSeconds(1f);
        Collider[] l_Colliders = new Collider[5];
        int l_Collisions = 0;
        
        l_Collisions = Physics.OverlapSphereNonAlloc(transform.position, 0.15f, l_Colliders, i_PropLayerMask);

        for (int i = 0; i < l_Collisions; i++)
        {
            if (l_Colliders[i].GetComponentInParent<ServerSideHealth>())
            {
                l_Colliders[i].GetComponentInParent<ServerSideHealth>().Damage(1);
            }
        }
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        StartCoroutine(LightningDelay());
    }
    
    private IEnumerator LightningDelay()
    {
        // INFO: warning sound plays on awake
        yield return new WaitForSeconds(1f);
        i_Audio.pitch += Random.Range(-0.05f, 0.05f);
        i_Audio.PlayOneShot(i_LightningSound);
        i_LightningVisuals.SetActive(true);
        yield return new WaitForSeconds(0.5f);
        i_LightningVisuals.SetActive(false);
    }
}
