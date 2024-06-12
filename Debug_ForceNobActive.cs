using System.Collections;
using System.Collections.Generic;
using FishNet.Object;
using UnityEngine;

public class Debug_ForceNobActive : NetworkBehaviour
{
    [SerializeField] private NetworkObject i_Nob;
    public override void OnStartClient()
    {
        base.OnStartClient();
        i_Nob.gameObject.SetActive(true);
    }
}
