using System;
using System.Collections;
using System.Collections.Generic;
using FishNet.Object.Synchronizing;
using FishNet.Transporting;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PropUI : MonoBehaviour
{
    private static PropUI i_Instance;
    
    [SerializeField] private Image i_CurrentPropImage;
    [SerializeField] private Sprite i_BarrelSprite, i_CrateSprite, i_ColumnSprite, i_ShrubSprite;
    [SerializeField] private TextMeshProUGUI i_PropText;
    [SerializeField] private GameObject[] i_PropHP;

    [SerializeField] private GameObject i_ObjectiveUI;
    [SerializeField] private Image i_ObjectiveTimerImage;
    [SerializeField] private TextMeshProUGUI i_ObjectiveText;
    private float i_ObjectiveTimerBase = 0f;
    

#region BOTH
    private void Awake()
    {
        if (i_Instance == null)
        {
            i_Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnEnable()
    {
        Health_Reset();
    }

    private void OnDisable()
    {
        Client_EndObjective();
    }

    #endregion BOTH    

#region CLIENT
    #region Prop
    public static void ChangeProp(string _prop)
    {
        i_Instance.i_CurrentPropImage.sprite = _prop switch
        {
            "Barrel" => i_Instance.i_BarrelSprite,
            "Crate" => i_Instance.i_CrateSprite,
            "Column" => i_Instance.i_ColumnSprite,
            "Shrub" => i_Instance.i_ShrubSprite,
            _ => i_Instance.i_CurrentPropImage.sprite
        };
        i_Instance.i_PropText.text = _prop;
    }
    #endregion Prop
    #region Health
    private void Health_Reset()
    {
        foreach (GameObject _bar in i_PropHP)
        {
            _bar.SetActive(true);
        }
    }
    public static void Health_OnChange(int _health)
    {
        i_Instance.i_PropHP[2-_health].SetActive(false); // DEBUG: works as long as hp is 3
    }
    #endregion Health
    #region Objective
    public void Client_StartObjective(string _objective, float _maxTime)
    {
        i_ObjectiveText.text = _objective;
        i_ObjectiveTimerBase = _maxTime;
        i_ObjectiveTimerImage.fillAmount = 1;
        i_ObjectiveUI.SetActive(true);
    }

    public void Client_UpdateObjectiveTimer(float _next)
    {
        i_ObjectiveTimerImage.fillAmount = _next / i_ObjectiveTimerBase;
        if(_next <= 0)
        {
            Client_EndObjective();
        }
    }

    public void Client_EndObjective()
    {
        i_ObjectiveUI.SetActive(false);
    }
    #endregion Objective
#endregion CLIENT
}
