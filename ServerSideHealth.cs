using System;
using System.Collections;
using System.Collections.Generic;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Transporting;
using TMPro;
using UnityEngine;
using UnityEngine.Assertions.Must;

public class ServerSideHealth : NetworkBehaviour
{
    public int p_BaseHealth = 100;
    private readonly SyncVar<int> i_CurrentHealth = new SyncVar<int>(new SyncTypeSettings(WritePermission.ServerOnly, ReadPermission.OwnerOnly, 0.5f, Channel.Unreliable));
    
    private bool i_Dying;

    private bool i_Subscribed;
    [SerializeField] private Light i_Flashlight;

    private void Awake()
    {
        i_CurrentHealth.Value = p_BaseHealth;
    }
    
    public override void OnStartNetwork()
    {
        base.OnStartNetwork();

        if(base.Owner.IsLocalClient || (base.IsServerInitialized && !Owner.IsValid))
        {
            i_Subscribed = true;
            i_CurrentHealth.OnChange += SyncVar_OnHealthChange;
        }
    }
    
    public override void OnStopNetwork()
    {
        base.OnStopNetwork();

        if (!i_Subscribed)
        {
            return;
        }
        
        i_CurrentHealth.OnChange -= SyncVar_OnHealthChange;
    }

    public void Heal(int _amount)
    {
        if (i_Dying)
        {
            return;
        }
        
        i_CurrentHealth.Value = Mathf.Clamp(i_CurrentHealth.Value + _amount, 0, p_BaseHealth);
    }

    public bool Damage(int _amount)
    {
        if (i_Dying)
        {
            return false;
        }
        
        i_CurrentHealth.Value -= _amount;
        
        if (i_CurrentHealth.Value <= 0)
        {
            i_Dying = true;
            
            PlayerSpawnManager.OnPropDeath(base.Owner, false);
                
            ServerManager.Despawn(gameObject);
            return true;
        }

        return false;
    }

    private void SyncVar_OnHealthChange(int prev, int next, bool asServer)
    {
        /* Each callback for SyncVars must contain a parameter
         * for the previous value, the next value, and asServer.
         * The previous value will contain the value before the
         * change, while next contains the value after the change.
         * By the time the callback occurs the next value had
         * already been set to the field, eg: _health.
         * asServer indicates if the callback is occurring on the
         * server or on the client. Sometimes you may want to run
         * logic only on the server, or client. The asServer
         * allows you to make this distinction. */
        
        if (!asServer)
        {
            PropUI.Health_OnChange(next);
            switch (next)
            {
                case 2:
                    i_Flashlight.intensity /= 2f;
                    break;
                case 1:
                    i_Flashlight.intensity /= 2f;
                    break;
                case 0:
                    i_Flashlight.intensity /= 2f;
                    break;
            }
        }
    }
}
