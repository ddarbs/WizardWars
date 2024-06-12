using System.Collections;
using System.Collections.Generic;
using FishNet.Object;
using UnityEngine;

public class SpectatorUI : MonoBehaviour
{
    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            PlayerSpawnManager.ServerSwapSpectatorCamera();
        }
    }
}
