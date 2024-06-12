using System.Collections;
using System.Collections.Generic;
using FishNet.Transporting.Tugboat;
using UnityEngine;

public class IPInputField : MonoBehaviour
{
    [SerializeField] private Tugboat i_Tugboat;

    public void InputField_OnValueChange(string _value)
    {
        i_Tugboat.SetClientAddress(_value == "" ? "localhost" : _value);
    }
}
