using System;
using System.Collections;
using System.Collections.Generic;
using FishNet.Connection;
using FishNet.Object;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

public class LobbyPlayer : NetworkBehaviour
{
    private bool i_Ready;
    
    [SerializeField] private TextMeshProUGUI i_NameText;
    [SerializeField] private Image[] i_Backgrounds;
    [SerializeField] private GameObject i_MyLobbyPlayer;
    private Button i_ReadyButton;
    private TMP_InputField i_NameField;

    [SerializeField] private Color i_Red, i_Green;
    
    // this is so stupid lol
    [SerializeField] private string[] i_Prefixes;
    [SerializeField] private string[] i_Suffixes;

    private void OnEnable()
    {
        foreach (var l_BG in i_Backgrounds)
        {
            l_BG.color = i_Red;
        }
        i_Ready = false;
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        if (!base.IsOwner)
        {
            return;
        }
        
        i_MyLobbyPlayer.SetActive(true);
        i_ReadyButton = GameObject.FindWithTag("ReadyButton").GetComponent<Button>();
        i_ReadyButton.onClick.AddListener(Client_ChangeReady);
        i_NameField = GameObject.FindWithTag("NameInput").GetComponent<TMP_InputField>();
        i_NameField.onEndEdit.AddListener(Client_UpdateName);
        Server_RandomName(base.LocalConnection);
    }

    [ServerRpc(RequireOwnership = false)]
    private void Server_RandomName(NetworkConnection _conn)
    {
        string l_Name = i_Prefixes[Random.Range(0, i_Prefixes.Length)];
        l_Name += i_Suffixes[Random.Range(0, i_Suffixes.Length)];
        l_Name += $"_{Random.Range(0, 421)}";
        
        i_NameText.text = l_Name;
        Observer_UpdateName(l_Name);
        Target_UpdateNameField(base.Owner, l_Name);
        PlayerSpawnManager.Server_UpdateLobbyName(_conn, l_Name);
    }

    private void Client_UpdateName(string _name)
    {
        Server_UpdateName(base.LocalConnection, _name);
    }

    [ServerRpc(RequireOwnership = false)]
    private void Server_UpdateName(NetworkConnection _conn, string _name)
    {
        if (!PlayerSpawnManager.Server_CheckDuplicateName(_name))
        {
            i_NameText.text = _name;
            PlayerSpawnManager.Server_UpdateLobbyName(_conn, _name);
            Observer_UpdateName(_name);
        }
        else
        {
            Target_UpdateNameField(_conn, i_NameText.text);
        }
    }

    [ObserversRpc(BufferLast = true)]
    private void Observer_UpdateName(string _name)
    {
        i_NameText.text = _name;
    }

    [TargetRpc]
    private void Target_UpdateNameField(NetworkConnection _conn, string _name)
    {
        i_NameField.text = _name;
    }

    private void Client_ChangeReady()
    {
        Server_ChangeReady(base.LocalConnection);
    }

    [ServerRpc(RequireOwnership = false)]
    private void Server_ChangeReady(NetworkConnection _conn)
    {
        i_Ready = !i_Ready;
        Observer_ChangeReady(i_Ready);
        switch (i_Ready)
        {
            case true:
                foreach (var l_BG in i_Backgrounds)
                {
                    l_BG.color = i_Green;
                }
                break;
            case false:
                foreach (var l_BG in i_Backgrounds)
                {
                    l_BG.color = i_Red;
                }
                break;
        }
    }

    [ObserversRpc(BufferLast = true)]
    private void Observer_ChangeReady(bool _ready)
    {
        switch (_ready)
        {
            case true:
                foreach (var l_BG in i_Backgrounds)
                {
                    l_BG.color = i_Green;
                }
                break;
            case false:
                foreach (var l_BG in i_Backgrounds)
                {
                    l_BG.color = i_Red;
                }
                break;
        }
    }

    public void Server_GameStarted()
    {
        foreach (var l_BG in i_Backgrounds)
        {
            l_BG.color = i_Red;
        }
        i_Ready = false;
    }
}
