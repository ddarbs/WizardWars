using System.Collections;
using System.Collections.Generic;
using FishNet;
using FishNet.Object;
using FishNet.Utility.Performance;
using UnityEngine;

public class PropManager : NetworkBehaviour
{
    [SerializeField] private NetworkObject i_CrateProp, i_BarrelProp;

    [SerializeField] private Transform[] i_MapProps;
    
    public override void OnStartServer()
    {
        if (!IsServerInitialized)
        {
            enabled = false;
            gameObject.SetActive(false);
            return;
        }
        
        RotateProps();
    }


    private void RotateProps()
    {
        foreach (Transform _prop in i_MapProps)
        {
            _prop.localEulerAngles = new Vector3(0, Random.Range(0, 360), 0);
            _prop.localPosition += new Vector3(Random.Range(-0.1f, 0.1f), 0, Random.Range(-0.1f, 0.1f));
        }
    }
}
