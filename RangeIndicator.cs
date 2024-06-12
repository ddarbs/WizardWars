using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class RangeIndicator : MonoBehaviour
{
    private static RangeIndicator i_Instance;

    [SerializeField] private Sprite i_CanHit, i_CanNotHit;
    [SerializeField] private Image i_RangeIndicator;
    
    private void Awake()
    {
        i_Instance = this;
    }

    private void OnEnable()
    {
        i_Instance = this;
    }

    public static void CanHit()
    {
        i_Instance.i_RangeIndicator.sprite = i_Instance.i_CanHit;
    }

    public static void CanNotHit()
    {
        i_Instance.i_RangeIndicator.sprite = i_Instance.i_CanNotHit;
    }
}
