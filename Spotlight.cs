using System.Collections;
using System.Collections.Generic;
using FishNet.Object;
using PlayFlow;
using UnityEngine;

public class Spotlight : NetworkBehaviour
{
    public override void OnStartServer()
    {
        base.OnStartServer();
        StartCoroutine(DespawnDelay());
    }
    
    // TODO: add audio for clients via observer?

    private IEnumerator DespawnDelay()
    {
        yield return new WaitForSeconds(5f);
        ServerManager.Despawn(gameObject);
    }
}
