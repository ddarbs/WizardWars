using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AbilityUI : MonoBehaviour
{
    [SerializeField] private List<GameObject> i_AbilityObjects = new List<GameObject>();

    [SerializeField] private GameObject i_PropPrefab, i_SeekerPrefab;
    [SerializeField] private Transform i_DisplayParent;

    private static AbilityUI i_Instance;

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

    public static void GainPropAbility(bool _roundStart)
    {
        if (_roundStart && i_Instance.i_DisplayParent.childCount > 0)
        {
            i_Instance.CleanAbilityUI();
        }
        GameObject i_Display = Instantiate(i_Instance.i_PropPrefab, i_Instance.i_DisplayParent);
        i_Instance.i_AbilityObjects.Add(i_Display);
    }
    
    public static void GainSeekerAbility(bool _roundStart)
    {
        if (_roundStart && i_Instance.i_DisplayParent.childCount > 0)
        {
            i_Instance.CleanAbilityUI();
        }
        GameObject i_Display = Instantiate(i_Instance.i_SeekerPrefab, i_Instance.i_DisplayParent);
        i_Instance.i_AbilityObjects.Add(i_Display);
    }

    private void CleanAbilityUI()
    {
        for (int i = 0; i < i_DisplayParent.childCount; i++)
        {
            Destroy(i_DisplayParent.GetChild(i).gameObject);
        }
        i_AbilityObjects.Clear();
    }

    public static void LoseAbility()
    {
        Destroy(i_Instance.i_AbilityObjects[i_Instance.i_AbilityObjects.Count - 1]);
        i_Instance.i_AbilityObjects.RemoveAt(i_Instance.i_AbilityObjects.Count - 1);
    }
}
