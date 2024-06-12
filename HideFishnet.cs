using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HideFishnet : MonoBehaviour
{
    [SerializeField] private GameObject i_HUD;

    public void HideHud()
    {
        i_HUD.SetActive(false);
    }
}
