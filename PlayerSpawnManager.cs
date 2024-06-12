using System;
using System.Collections;
using System.Collections.Generic;
using FishNet;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Transporting;
using GameKit.Dependencies.Utilities;
using TMPro;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using Random = UnityEngine.Random;

public class PlayerSpawnManager : NetworkBehaviour
{
    private const int c_MaxSeekers = 1;
    private const float c_RoundTime = 120f;
    private const int c_MapPropsToSpawn = 40;
    private const float c_SpawnOpenRadius = 0.4f;
    private readonly Vector2 c_SpawnBounds = new Vector2(7f, 6f);

    [SerializeField] private NetworkObject i_PropPrefab, i_SeekerPrefab;
    
    private int i_Props, i_Seekers;

    [SerializeField] private GameObject i_LobbyUI, i_SeekerUI, i_PropUI, i_SpectateUI, i_KillFeedUI, i_AbilityUI, i_PreSeekerUI;
    [SerializeField] private Button i_ReadyButton;
    [SerializeField] private TextMeshProUGUI i_ReadyText, i_ReadyCountText, i_RoleText;
    [SerializeField] private Image i_TimerImage;

    private bool i_WatchingReadyUps;

    private List<NetworkConnection> i_ReadyUps = new List<NetworkConnection>();
    private Dictionary<NetworkConnection, Lobby> i_LobbyDict = new Dictionary<NetworkConnection, Lobby>();
    private Dictionary<NetworkConnection, NetworkObject> i_PlayerDict = new Dictionary<NetworkConnection, NetworkObject>();

    private static PlayerSpawnManager i_Instance;
    
    private readonly SyncVar<float> i_RoundTime = new SyncVar<float>(new SyncTypeSettings(WritePermission.ServerOnly, ReadPermission.Observers, 0.5f, Channel.Unreliable));

    private LayerMask i_PropSpawnLayerMask;

    [SerializeField] private NetworkObject i_BarrelPrefab, i_CratePrefab, i_ColumnPrefab, i_ShrubPrefab;

    [SerializeField] private NetworkObject i_LightningPrefab;

    [SerializeField] private NetworkObject i_FindTheItemPrefab, i_SpotlightPrefab;
    private NetworkObject i_FindTheItemObject;

    private List<NetworkObject> i_MapProps = new List<NetworkObject>();
    
    private int i_SpectatingIndex = -1;

    private GameObject i_CurrentSpectatorCamera;

    private bool i_ObjectiveActive = false;
    private int i_ObjectivePropIndex = -1;
    private bool i_ObjectiveItemFound = false;

    [SerializeField] private AudioSource i_Audio;
    [SerializeField] private AudioClip i_FoundItemSFX, i_NewObjectiveSFX, i_SeekerSpawnSFX;

    [SerializeField] private PropUI i_PropUIScript;
    [SerializeField] private NetworkObject i_LobbyPlayerPrefab;
    [SerializeField] private NetworkObject i_LobbyPlayerParent;
    
    public readonly SyncVar<int> i_ObjectiveTimer = new SyncVar<int>(new SyncTypeSettings(WritePermission.ServerOnly, ReadPermission.Observers, 0.5f, Channel.Unreliable));

    [SerializeField] private GameObject i_KillFeedPrefab;
    [SerializeField] private Transform i_KillFeedParent;

    private string i_SeekerName; // im an idiot
    [SerializeField] private TMP_InputField i_NameInput;

    [SerializeField] private Image[] i_PropHPBars;
    private bool i_SentTargetLowHP;
    [SerializeField] private AudioSource i_LobbyAudio;
    
    private void SyncVar_OnTimerChange(int prev, int next, bool asServer)
    {
        if (!asServer)
        {
            i_PropUIScript.Client_UpdateObjectiveTimer(next);
        }
    }
    
    [Serializable]
    public struct Player
    {
        public NetworkObject _player;
        public GameObject _camera;
    }
    
    [Serializable]
    public class Lobby
    {
        public NetworkObject _lobby;
        public string _name;
    }
    
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
        
        i_PropSpawnLayerMask = LayerMask.GetMask("Wall", "Prop", "Player");
    }

    public override void OnStartNetwork()
    {
        base.OnStartNetwork();
        
        i_RoundTime.OnChange += SyncVar_OnTimeChange;
        i_ObjectiveTimer.OnChange += SyncVar_OnTimerChange;
    }
    
    public override void OnStopNetwork()
    {
        base.OnStopNetwork();
        
        i_RoundTime.OnChange -= SyncVar_OnTimeChange;
        i_ObjectiveTimer.OnChange -= SyncVar_OnTimerChange;
    }

    public override void OnStartServer()
    {
        if (i_PropPrefab == null || i_SeekerPrefab == null)
        {
            Debug.LogError("Player Prefabs aren't assigned", this);
            return;
        }
        i_LobbyUI.SetActive(true);
        i_SeekerUI.SetActive(true);
        i_PropUI.SetActive(true);
        i_KillFeedUI.SetActive(true);
        NetworkManager.SceneManager.OnClientLoadedStartScenes += OnClientLoadedStartScenes;
        
        ServerManager.OnRemoteConnectionState += OnPlayerConnectionChange;
    }
    
    private void OnPlayerConnectionChange(NetworkConnection _conn, RemoteConnectionStateArgs _args)
    {
        switch (_args.ConnectionState)
        {
            case RemoteConnectionState.Started:
                break;
            case RemoteConnectionState.Stopped:
                if (i_LobbyDict.ContainsKey(_conn))
                {
                    i_LobbyDict.Remove(_conn);
                }
                if (i_PlayerDict.Count > 2) // if there are more than two alive players (so props can maybe quit without ending round for others) 
                {
                    if (i_PlayerDict.ContainsKey(_conn)) 
                    {
                        if (i_PlayerDict[_conn].GetComponent<PlayerSeeker>()) // if the quitter is the seeker
                        {
                            EndRound();
                        }
                        else // if the quitter is a prop 
                        {
                            ServerManager.Despawn(i_PlayerDict[_conn]);
                            OnPropDeath(_conn, true);
                        }
                    }
                    else // seeker is still spawning
                    {
                        EndRound();
                    }
                    ObserverReadyUp(i_ReadyUps.Count, ServerManager.Clients.Count - 1); // INFO: seems like quitter still counted as client at this point
                }
                else if (i_PlayerDict.Count > 0) // if there are only 2 players and one leaves you can't finish the game
                {
                    EndRound();
                    ObserverReadyUp(i_ReadyUps.Count, ServerManager.Clients.Count - 1); // INFO: seems like quitter still counted as client at this point
                }
                else // quit happened during the lobby
                {
                    if (i_ReadyUps.Contains(_conn))
                    {
                        i_ReadyUps.Remove(_conn);
                    }
                    ObserverReadyUp(i_ReadyUps.Count, ServerManager.Clients.Count - 1); // INFO: seems like quitter still counted as client at this point
                }
                break;
        }
    }

    private IEnumerator ReadyWatchThread()
    {
        i_WatchingReadyUps = true;
        while (i_ReadyUps.Count < 2)
        {
            yield return new WaitForSeconds(1f);
        }

        bool i_Cancel = false;
        for (int i = 5; i >= 0; i--)
        {
            if (i_ReadyUps.Count < 2)
            {
                i_Cancel = true;
                break;
            }

            if (i == 1)
            {
                foreach (var _lp in i_LobbyDict.Keys)
                {
                    Target_ChangeReadyUpButton(_lp, false);
                }
            }
            i_ReadyText.text = "starting in " + i;
            Observer_ReadyText(i_ReadyText.text);
            yield return new WaitForSeconds(1f);
        }
        
        if (i_ReadyUps.Count >= 2)
        {
            StartRound();
            i_ReadyUps.Clear();
        }
        
        i_ReadyText.text = "";
        Observer_ReadyText(i_ReadyText.text);
        
        if (i_Cancel)
        {
            foreach (var _lp in i_LobbyDict.Keys)
            {
                Target_ChangeReadyUpButton(_lp, true);
            }
            StartCoroutine(ReadyWatchThread());
            yield break;
        }
        
        i_WatchingReadyUps = false;
    }

    [TargetRpc]
    private void Target_ChangeReadyUpButton(NetworkConnection _conn, bool _interactable)
    {
        i_ReadyButton.interactable = _interactable;
        i_NameInput.interactable = _interactable;
    }

    [ObserversRpc(BufferLast = true)]
    private void Observer_ReadyText(string _text)
    {
        i_ReadyText.text = _text; 
    }

    public override void OnStopServer()
    {
        StopAllCoroutines();
        if (i_PropPrefab != null && i_SeekerPrefab != null)
        {
            NetworkManager.SceneManager.OnClientLoadedStartScenes -= OnClientLoadedStartScenes;
        }
    }

    private void OnClientLoadedStartScenes(NetworkConnection _conn, bool _server)
    {
        if (!_server) // INFO: shouldn't even need this cause only server subs this event
        {
            return;
        }

        if (!i_LobbyDict.ContainsKey(_conn))
        {
            NetworkObject l_LobbyPlayer = Instantiate(i_LobbyPlayerPrefab);
            l_LobbyPlayer.SetParent(i_LobbyPlayerParent);
            l_LobbyPlayer.transform.localScale = new Vector3(1f, 1f, 1f); // BUG: for some reason playflow scales them up to (3, 3, 3) lol 
            ServerManager.Spawn(l_LobbyPlayer, _conn);
            SceneManager.AddOwnerToDefaultScene(l_LobbyPlayer);
            i_LobbyDict.Add(_conn, new Lobby()
            {
                _lobby = l_LobbyPlayer,
                _name = "bingbong"
            });
        }
        
        if (i_PlayerDict.Count > 0) // round in progress
        {
            // shoot them in as spectators
            TargetEnableSpectateUI(_conn);
            TargetJoinDuringRound(_conn);
        }
        else
        {
            TargetResetRound(_conn); // BUG: temp fix for button/text if someone readied up then hit the client button and rejoined
            TargetEnableLobbyUI(_conn);
        
            ObserverReadyUp(i_ReadyUps.Count, ServerManager.Clients.Count);

            if (!i_WatchingReadyUps)
            {
                StartCoroutine(ReadyWatchThread());
            } 
        }
    }

    [TargetRpc]
    private void TargetJoinDuringRound(NetworkConnection _conn)
    {
        ServerSwapSpectatorCamera();
    }

    [TargetRpc]
    private void TargetEnableLobbyUI(NetworkConnection _conn)
    {
        i_SeekerUI.SetActive(false);
        i_PropUI.SetActive(false);
        i_SpectateUI.SetActive(false);
        i_KillFeedUI.SetActive(false);
        i_AbilityUI.SetActive(false);
        i_LobbyUI.SetActive(true);
        i_LobbyAudio.Play();
        
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void Button_ReadyUp()
    {
        ServerReadyUp(base.LocalConnection);
    }
    
    [ServerRpc(RequireOwnership = false)]
    private void ServerReadyUp(NetworkConnection _conn)
    {
        if (i_ReadyUps.Contains(_conn))
        {
            i_ReadyUps.Remove(_conn);
        }
        else
        {
            i_ReadyUps.Add(_conn);
        }
        ObserverReadyUp(i_ReadyUps.Count, ServerManager.Clients.Count);
    }

    [ObserversRpc(BufferLast = true)]
    private void ObserverReadyUp(int _ready, int _players)
    {
        if (_players == _ready && _players >= 2)
        {
            i_ReadyCountText.color = Color.green;
        }
        else if (_players < 2)
        {
            i_ReadyCountText.color = Color.yellow;
        }
        else
        {
            i_ReadyCountText.color = Color.white;
        }
        i_ReadyCountText.text = $"{_ready}/{_players} ready";
    }
    
    private void StartRound()
    {
        SpawnMapProps();
        
        // INFO: Setup seeker or prop
        List<NetworkConnection> l_Props = new List<NetworkConnection>();
        NetworkConnection l_Seeker = default;
        int l_Index = 0;
        
        foreach (var _player in i_ReadyUps)
        {
            if (l_Index == i_ReadyUps.Count - 1 && i_Seekers == 0)
            {
                i_Seekers++;
                l_Seeker = _player;
            }
            else if (i_Seekers == 0)
            {
                switch (Random.Range(0, 2))
                {
                    case 0:
                        i_Props++;
                        l_Props.Add(_player);
                        break;
                    case 1:
                        i_Seekers++;
                        l_Seeker = _player;
                        break;
                }
            }
            else
            {
                i_Props++;
                l_Props.Add(_player);
            }
            l_Index++;
        }

        // INFO: Spawn props in
        NetworkObject l_Player = default;
        foreach (var _prop in l_Props)
        {
            TargetEnablePropUI(_prop);
            
            l_Player = Instantiate(i_PropPrefab, Vector3.zero, Quaternion.Euler(0f, Random.Range(0, 360), 0f));
            ServerManager.Spawn(l_Player, _prop);
            SceneManager.AddOwnerToDefaultScene(l_Player);

            i_PlayerDict.Add(_prop, l_Player);
        }
        
        // TODO: activate pre-seeker ui
        i_SeekerName = i_LobbyDict[l_Seeker]._name;
        
        
        // INFO: spawn spectators in
        foreach (var _spec in i_LobbyDict.Keys)
        {
            if (i_PlayerDict.ContainsKey(_spec) || _spec == l_Seeker) continue;
            
            TargetEnableSpectateUI(_spec);
            TargetJoinDuringRound(_spec);
        }
        
        // TODO: add some spooky countdown shit for everyone while seeker waits to spawn
        StartCoroutine(DelaySeekerSpawn(l_Seeker));
        
        foreach (var _lp in i_LobbyDict.Values)
        {
            _lp._lobby.GetComponent<LobbyPlayer>().Server_GameStarted();
        }
    }

    private IEnumerator DelaySeekerSpawn(NetworkConnection _seeker)
    {
        TargetSeekerPreSpawnUI(_seeker);
        yield return new WaitForSeconds(14.25f); // INFO: 15s for props to hide?

        Observer_SeekerSpawnSFX();
        
        yield return new WaitForSeconds(0.75f);
        
        if (_seeker == null)
        {
            EndRound();
        }
        else
        {
            TargetEnableSeekerUI(_seeker);
            NetworkObject l_Player = Instantiate(i_SeekerPrefab, Vector3.zero, Quaternion.Euler(0f, Random.Range(0, 360), 0f));
            ServerManager.Spawn(l_Player, _seeker);
            SceneManager.AddOwnerToDefaultScene(l_Player);
            
            i_PlayerDict.Add(_seeker, l_Player);

            StartCoroutine(RoundTimer());
        }
    }

    [ObserversRpc]
    private void Observer_SeekerSpawnSFX()
    {
        i_Audio.PlayOneShot(i_SeekerSpawnSFX, 0.5f);
    }
    
    [TargetRpc]
    private void TargetSeekerPreSpawnUI(NetworkConnection _conn)
    {
        i_LobbyUI.SetActive(false);
        i_PreSeekerUI.SetActive(true);
    }
    
    private IEnumerator RoundTimer() // INFO: also ends round if all props die 
    {
        i_RoundTime.Value = c_RoundTime;
        float l_Rate = 1f;

        StartCoroutine(ObjectiveTimer());
        
        while (i_Props > 0 && i_RoundTime.Value > 0f)
        {
            yield return new WaitForSeconds(l_Rate);
            i_RoundTime.Value -= l_Rate;
            if (i_RoundTime.Value <= 60 && !i_SentTargetLowHP)
            {
                i_SentTargetLowHP = true;
                foreach (var _player in i_PlayerDict)
                {
                    if (_player.Value.GetComponent<PlayerSeeker>())
                    {
                        continue;
                    }
                    Target_LowRoundTime(_player.Key);
                }
            }
        }

        EndRound();
    }

    [TargetRpc]
    private void Target_LowRoundTime(NetworkConnection _conn)
    {
        StartCoroutine(Client_LowRoundTimeHPBlink());
    }
    
    private IEnumerator Client_LowRoundTimeHPBlink()
    {
        Color l_Color = Color.white;
        bool l_Add = false;
        while (i_RoundTime.Value > 0)
        {
            switch (l_Color.a)
            {
                case <= 0.4f:
                    l_Add = true;
                    break;
                case >= 0.8f:
                    l_Add = false;
                    break;
            }
            switch (l_Add)
            {
                case true:
                    l_Color.a += 0.1f;
                    break;
                case false:
                    l_Color.a -= 0.1f;
                    break;
            }
            foreach (var _hp in i_PropHPBars)
            {
                _hp.color = l_Color;
            }
            yield return new WaitForSeconds(0.25f);
        }
    }
    
    public static void OnPropDeath(NetworkConnection _conn, bool _disconnect)
    {
        i_Instance.i_PlayerDict.Remove(_conn);
        i_Instance.i_Props--;
        if (i_Instance.i_Props > 0 && !_disconnect)
        {
            i_Instance.TargetEnableSpectateUI(_conn);
            i_Instance.i_SpectatingIndex++;
            
            if (i_Instance.i_SpectatingIndex == i_Instance.i_PlayerDict.Count)
            {
                i_Instance.i_SpectatingIndex = 0;
            }

            int l_Index = 0;
            foreach (var _player in i_Instance.i_PlayerDict)
            {
                if (l_Index == i_Instance.i_SpectatingIndex)
                {
                    i_Instance.TargetSwapSpectatorCamera(_conn, _player.Value);
                    break;
                }
                l_Index++;
            }
        }

        if (i_Instance.i_Props > 0)
        {
            i_Instance.Observer_KillFeedEntry(i_Instance.i_SeekerName, i_Instance.i_LobbyDict[_conn]._name);
        }
    }

    [ObserversRpc]
    private void Observer_KillFeedEntry(string _seeker, string _prop)
    {
        if (_seeker == "")
        {
            _seeker = "???";
        }
        string l_Text = $"<color=red>{_seeker}</color> <> <color=yellow>{_prop}</color>";
        GameObject l_Entry = Instantiate(i_KillFeedPrefab, i_KillFeedParent);
        l_Entry.GetComponent<KillFeed>().Setup(l_Text);
    }

    public static void Server_UpdateLobbyName(NetworkConnection _conn, string _name)
    {
        i_Instance.i_LobbyDict[_conn]._name = _name;
    }

    public static bool Server_CheckDuplicateName(string _name)
    {
        foreach (var _lp in i_Instance.i_LobbyDict)
        {
            if (_name == _lp.Value._name)
            {
                return true;
            }
        }

        return false;
    }

    private void EndRound()
    {
        StopAllCoroutines();
        
        // despawn remaining players 
        foreach (var _player in i_PlayerDict.Values)
        {
            ServerManager.Despawn(_player);
        }
        i_PlayerDict.Clear();
        
        // reset client stuff
        foreach (var _lp in i_LobbyDict.Keys)
        {
            Target_ChangeReadyUpButton(_lp, true);
        }
        i_ReadyUps.Clear(); 
        ObserverReadyUp(i_ReadyUps.Count, ServerManager.Clients.Count);
        i_SentTargetLowHP = false;
        
        
        // enable everyone's lobby ui
        foreach (var _player in ServerManager.Clients.Values)
        {
            TargetEnableLobbyUI(_player);
        }

        // reset map stuff 
        foreach (var _mapProp in i_MapProps)
        {
            ServerManager.Despawn(_mapProp);
        }
        i_MapProps.Clear();

        if (i_FindTheItemObject != null)
        {
            ServerManager.Despawn(i_FindTheItemObject);
        }
        
        i_ObjectiveActive = false;
        i_ObjectiveItemFound = false;
        i_RoundTime.Value = 0;
        i_Props = i_Seekers = 0;
        
        // wait for everyone to be ready
        if (ServerManager.Clients.Count >= 2 && !i_WatchingReadyUps)
        {
            StartCoroutine(ReadyWatchThread());
        }
    }

    [TargetRpc]
    private void TargetEnablePropUI(NetworkConnection _conn)
    {
        foreach (var _hp in i_PropHPBars)
        {
            _hp.color = Color.white;
        }
        i_LobbyUI.SetActive(false);
        i_AbilityUI.SetActive(true);
        i_KillFeedUI.SetActive(true);
        i_PropUI.SetActive(true);
    }
    
    [TargetRpc]
    private void TargetEnableSeekerUI(NetworkConnection _conn)
    {
        i_PreSeekerUI.SetActive(false);
        i_AbilityUI.SetActive(true);
        i_KillFeedUI.SetActive(true);
        i_SeekerUI.SetActive(true);
    }
    
    [TargetRpc]
    private void TargetEnableSpectateUI(NetworkConnection _conn)
    {
        i_LobbyUI.SetActive(false);
        i_PropUI.SetActive(false);
        i_AbilityUI.SetActive(false);
        i_KillFeedUI.SetActive(true);
        i_SpectateUI.SetActive(true);
        
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
    
    [TargetRpc]
    private void TargetResetRound(NetworkConnection _conn)
    {
        i_ReadyButton.interactable = true;
        i_NameInput.interactable = true;
    }

    private IEnumerator ObjectiveTimer()
    {
        yield return new WaitForSeconds(5f); // wait 5s before starting to do objs
        
        while (i_RoundTime.Value > 0f)
        {
            if (i_ObjectiveActive)
            {
                yield return new WaitForSeconds(1f);
                continue;
            }
            
            yield return new WaitForSeconds(5f); // 5s break between each obj
            
            i_ObjectiveActive = true;
            
            switch (Random.Range(0, 5))
            {
                case 0:
                    ObjectiveAssignedProp();
                    break;
                case 1:
                    ObjectiveFindItem();
                    break;
                case 2:
                    ObjectiveShuffle();
                    break;
                case 3:
                    ObjectiveBadShuffle();
                    break;
                case 4:
                    ObjectiveSpotlight();
                    break;
            }
        }
    }
    
    #region Obj - Find Item
    private void ObjectiveFindItem()
    {
        int l_Timer = 10;
        i_ObjectiveItemFound = false;
        
        bool l_ValidSpawnPoint = false;
        Vector3 l_SpawnPoint = Vector3.up;
        
        Collider[] l_Colliders = new Collider[5];
        int l_Collisions = 0;
        
        int l_Tries = 0;
        while(!l_ValidSpawnPoint)
        {
            l_SpawnPoint = new Vector3(Random.Range(-c_SpawnBounds.x, c_SpawnBounds.x), 0.1f, Random.Range(-c_SpawnBounds.y, c_SpawnBounds.y));
            l_Collisions = Physics.OverlapSphereNonAlloc(l_SpawnPoint, c_SpawnOpenRadius + 0.1f, l_Colliders, i_PropSpawnLayerMask);

            l_Tries++;
            if (l_Tries > 1000)
            {
                Debug.LogWarning($"Could not find a valid spawn point! spawning at 0,0,0");
                l_SpawnPoint = Vector3.zero;
                break;
            }
            
            if (l_Collisions > 0)
            {
                continue;
            }
            
            l_SpawnPoint.y = 0f;
            l_ValidSpawnPoint = true;
        }
        i_FindTheItemObject = Instantiate(i_FindTheItemPrefab, l_SpawnPoint, Quaternion.Euler(0f, Random.Range(0, 360), 0f));
        ServerManager.Spawn(i_FindTheItemObject);
        
        foreach (var _player in i_PlayerDict)
        {
            if (_player.Value.GetComponent<PlayerSeeker>())
            {
                // todo: assign seeker obj? refill seeker ability?
                TargetObjectiveFindItemSeeker(_player.Key, i_FindTheItemObject);
                continue;
            }
            TargetObjectiveFindItemProp(_player.Key, l_Timer, i_FindTheItemObject);
        }
        StartCoroutine(ObjectiveFindItemTimer(l_Timer));
    }

    private IEnumerator ObjectiveFindItemTimer(int _timer)
    {
        i_ObjectiveTimer.Value = _timer;
        StartCoroutine(ObjectiveTimerThread());

        while (i_ObjectiveTimer.Value > 0 && !i_ObjectiveItemFound)
        {
            yield return new WaitForSeconds(1);
        }

        if (i_FindTheItemObject != null)
        {
            ServerManager.Despawn(i_FindTheItemObject);
        }
        if (!i_ObjectiveItemFound && i_PlayerDict.Count > 1)
        {
            foreach (var _player in i_PlayerDict)
            {
                if (_player.Value.GetComponent<PlayerSeeker>())
                {
                    continue;
                }

                _player.Value.GetComponent<ServerSideHealth>().Damage(1);
            }
        }
        
        i_ObjectiveActive = false;
    }
    
    [TargetRpc]
    private void TargetObjectiveFindItemProp(NetworkConnection _conn, float _maxtime, NetworkObject _item)
    {
        i_PropUIScript.Client_StartObjective("find the <color=purple>chest</color> or <color=red>else</color>", _maxtime);
        i_Audio.PlayOneShot(i_NewObjectiveSFX);
    }
    
    [TargetRpc]
    private void TargetObjectiveFindItemSeeker(NetworkConnection _conn, NetworkObject _item)
    {
        _item.gameObject.SetActive(false);
    }

    public static void ObjectiveFoundItem()
    {
        i_Instance.i_ObjectiveItemFound = true;
        i_Instance.ObserverObjectiveFoundItem();
    }

    [ObserversRpc]
    private void ObserverObjectiveFoundItem()
    {
        i_Audio.PlayOneShot(i_Instance.i_FoundItemSFX, 0.5f);
        i_PropUIScript.Client_EndObjective();
    }
    #endregion Obj - Find Item
    
    #region Obj - Assigned Prop
    private void ObjectiveAssignedProp()
    {
        int l_Timer = 15;
        i_ObjectivePropIndex = Random.Range(0, 4);
        foreach (var _player in i_PlayerDict)
        {
            if (_player.Value.GetComponent<PlayerSeeker>())
            {
                // todo: assign seeker obj? refill seeker ability?
                continue;
            }
            
            TargetObjectiveAssignedProp(_player.Key, i_ObjectivePropIndex, l_Timer);
        }

        StartCoroutine(ObjectiveAssignedPropTimer(l_Timer));
    }

    private IEnumerator ObjectiveTimerThread()
    {
        while (i_ObjectiveTimer.Value > 0 && i_ObjectiveActive)
        {
            yield return new WaitForSeconds(1f);
            i_ObjectiveTimer.Value--;
        }
    }

    private IEnumerator ObjectiveAssignedPropTimer(int _timer)
    {
        i_ObjectiveTimer.Value = _timer;
        StartCoroutine(ObjectiveTimerThread());
        
        yield return new WaitForSeconds(5f); // DEBUG: placeholder time before it checks

        int l_Rate = 2;
        while (i_ObjectiveTimer.Value > 0 && i_PlayerDict.Count > 0)
        {
            foreach (var _player in i_PlayerDict)
            {
                if (_player.Value.GetComponent<PlayerSeeker>())
                {
                    continue;
                }

                if (_player.Value.GetComponent<PlayerProp>().p_PropIndex != i_ObjectivePropIndex)
                {
                    StartCoroutine(Lightning(_player.Value.transform.position));
                }
            }
            
            yield return new WaitForSeconds(l_Rate);
        }
        
        i_ObjectivePropIndex = -1;
        i_ObjectiveActive = false;
    }

    private IEnumerator Lightning(Vector3 _pos)
    {
        NetworkObject l_Lightning = Instantiate(i_LightningPrefab, _pos, Quaternion.Euler(0f, Random.Range(0, 360), 0f));
        ServerManager.Spawn(l_Lightning);
        yield return new WaitForSeconds(3f);
        ServerManager.Despawn(l_Lightning);
    }

    [TargetRpc]
    private void TargetObjectiveAssignedProp(NetworkConnection _conn, int _propIndex, float _maxTime)
    {
        string l_Objective = "be a <color=yellow>";
        switch (_propIndex)
        {
            case 0:
                l_Objective += "barrel";
                break;
            case 1:
                l_Objective += "crate";
                break;
            case 2:
                l_Objective += "column";
                break;
            case 3:
                l_Objective += "shrub";
                break;
        }
        l_Objective += "</color> or <color=red>else</color>";
        i_PropUIScript.Client_StartObjective(l_Objective, _maxTime);
        i_Audio.PlayOneShot(i_NewObjectiveSFX);
    }
    #endregion Obj - Assigned Prop
    
    #region Obj - Shuffle

    private void ObjectiveShuffle()
    {
        int l_Timer = 5;
        
        foreach (var _player in i_PlayerDict)
        {
            if (_player.Value.GetComponent<PlayerSeeker>())
            {
                continue;
            }
            
            TargetObjectiveShuffle(_player.Key, l_Timer);
        }
        
        StartCoroutine(ObjectiveShuffleTimer(l_Timer));
    }

    private IEnumerator ObjectiveShuffleTimer(int _timer)
    {
        i_ObjectiveTimer.Value = _timer;
        StartCoroutine(ObjectiveTimerThread());
        
        while (i_ObjectiveTimer.Value > 0 && i_ObjectiveActive)
        {
            yield return new WaitForSeconds(1f);
        }

        if (i_MapProps.Count > 0)
        {
            foreach (var _mapProp in i_MapProps)
            {
                _mapProp.GetComponent<Rigidbody>().AddForce(Random.Range(-100f, 100f), 0f, Random.Range(-100f, 100f));
            }
        }
        i_ObjectiveActive = false;
    }
    
    [TargetRpc]
    private void TargetObjectiveShuffle(NetworkConnection _conn, float _maxTime)
    {
        string l_Objective = "let's mix <color=yellow>things</color> up";
        i_PropUIScript.Client_StartObjective(l_Objective, _maxTime);
        i_Audio.PlayOneShot(i_NewObjectiveSFX);
    }
    #endregion Obj - Shuffle
    
    #region Obj - Bad Shuffle

    private void ObjectiveBadShuffle()
    {
        int l_Timer = 5;
        
        foreach (var _player in i_PlayerDict)
        {
            if (_player.Value.GetComponent<PlayerSeeker>())
            {
                continue;
            }
            
            TargetObjectiveBadShuffle(_player.Key, l_Timer);
        }
        
        StartCoroutine(ObjectiveBadShuffleTimer(l_Timer));
    }

    private IEnumerator ObjectiveBadShuffleTimer(int _timer)
    {
        i_ObjectiveTimer.Value = _timer;
        StartCoroutine(ObjectiveTimerThread());
        
        while (i_ObjectiveTimer.Value > 0 && i_ObjectiveActive)
        {
            yield return new WaitForSeconds(1f);
        }

        if (i_PlayerDict.Count > 1)
        {
            foreach (var _player in i_PlayerDict)
            {
                if (_player.Value.GetComponent<PlayerSeeker>())
                {
                    continue;
                }
                
                _player.Value.GetComponent<Rigidbody>().AddForce(Random.Range(-100f, 100f), 0f, Random.Range(-100f, 100f));
            }
        }
        i_ObjectiveActive = false;
    }
    
    [TargetRpc]
    private void TargetObjectiveBadShuffle(NetworkConnection _conn, float _maxTime)
    {
        string l_Objective = "let's mix <color=red>things</color> up";
        i_PropUIScript.Client_StartObjective(l_Objective, _maxTime);
        i_Audio.PlayOneShot(i_NewObjectiveSFX);
    }
    #endregion Obj - Bad Shuffle
    
    #region Obj - Spotlight
    private void ObjectiveSpotlight()
    {
        int l_Timer = 5;
        
        foreach (var _player in i_PlayerDict)
        {
            if (_player.Value.GetComponent<PlayerSeeker>())
            {
                continue;
            }
            
            TargetObjectiveSpotlight(_player.Key, l_Timer);
        }
        
        StartCoroutine(ObjectiveSpotlightTimer(l_Timer));
    }

    private IEnumerator ObjectiveSpotlightTimer(int _timer)
    {
        i_ObjectiveTimer.Value = _timer;
        StartCoroutine(ObjectiveTimerThread());
        
        while (i_ObjectiveTimer.Value > 0 && i_ObjectiveActive)
        {
            yield return new WaitForSeconds(1f);
        }

        if (i_PlayerDict.Count > 1)
        {
            NetworkObject l_Spotlight = null;
            Vector3 l_Spawnpoint = Vector3.up;
            foreach (var _player in i_PlayerDict)
            {
                if (_player.Value.GetComponent<PlayerSeeker>())
                {
                    continue;
                }

                if (_player.Value.GetComponent<Rigidbody>().velocity.magnitude > 0.65f)
                {
                    l_Spawnpoint = _player.Value.transform.position;
                    l_Spawnpoint.y = 0.1f;
                    l_Spotlight = Instantiate(i_SpotlightPrefab, l_Spawnpoint, Quaternion.Euler(90, 0, 0));
                    ServerManager.Spawn(l_Spotlight);
                }
            }
        }
        i_ObjectiveActive = false;
    }
    
    [TargetRpc]
    private void TargetObjectiveSpotlight(NetworkConnection _conn, float _maxTime)
    {
        string l_Objective = "<color=red>they</color> are <color=yellow>searching</color>";
        i_PropUIScript.Client_StartObjective(l_Objective, _maxTime);
        i_Audio.PlayOneShot(i_NewObjectiveSFX);
    }
    #endregion Obj - Spotlight
    
    private void SyncVar_OnTimeChange(float prev, float next, bool asServer)
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

        i_TimerImage.fillAmount = next / c_RoundTime;
        
        if (!asServer)
        {
            
        }
        else
        {
            
        }
    }

    public static void ServerSwapSpectatorCamera()
    {
        i_Instance.SwapSpectatorCamera(i_Instance.LocalConnection);
    }
    
    [ServerRpc(RequireOwnership = false)]
    private void SwapSpectatorCamera(NetworkConnection _conn)
    {
        i_SpectatingIndex++;
        
        if (i_SpectatingIndex >= i_PlayerDict.Count)
        {
            i_SpectatingIndex = 0;
        }

        int l_Index = 0;
        foreach (var _player in i_PlayerDict)
        {
            if (l_Index == i_SpectatingIndex)
            {
                TargetSwapSpectatorCamera(_conn, _player.Value);
                break;
            }
            l_Index++;
        }
    }

    [TargetRpc]
    private void TargetSwapSpectatorCamera(NetworkConnection _conn, NetworkObject _nextPlayer)
    {
        if (i_CurrentSpectatorCamera != null)
        {
            i_CurrentSpectatorCamera.SetActive(false);
        }
        
        if (_nextPlayer.GetComponent<PlayerSeeker>())
        {
            i_CurrentSpectatorCamera = _nextPlayer.GetComponent<PlayerSeeker>()._camera.gameObject;
        }
        else
        {
            i_CurrentSpectatorCamera = _nextPlayer.GetComponent<PlayerProp>()._camera.gameObject;
        }
        i_CurrentSpectatorCamera.SetActive(true);
    }
    
    private void SpawnMapProps()
    {
        Vector3 l_SpawnPoint = Vector3.up;
        Collider[] l_Colliders = new Collider[5];
        int l_Collisions = 0;
        NetworkObject l_PropPrefab = null;
        NetworkObject l_MapProp = null;
        int l_PropsToSpawn = c_MapPropsToSpawn - i_ReadyUps.Count - 1;
        int l_PropsSpawned = 0;
        int l_Tries = 0;
        
        while(l_PropsSpawned < l_PropsToSpawn)
        {
            l_SpawnPoint = new Vector3(Random.Range(-c_SpawnBounds.x, c_SpawnBounds.x), 0.1f, Random.Range(-c_SpawnBounds.y, c_SpawnBounds.y));
            l_Collisions = Physics.OverlapSphereNonAlloc(l_SpawnPoint, c_SpawnOpenRadius, l_Colliders, i_PropSpawnLayerMask);

            l_Tries++;
            if (l_Tries > 1000)
            {
                Debug.LogWarning($"Could only spawn {l_PropsSpawned}/{l_PropsToSpawn} map Props");
                return;
            }
            
            if (l_Collisions > 0)
            {
                continue;
            }
            
            switch (Random.Range(0, 4))
            {
                case 0:
                    l_PropPrefab = i_BarrelPrefab;
                    break;
                case 1:
                    l_PropPrefab = i_CratePrefab;
                    break;
                case 2:
                    l_PropPrefab = i_ColumnPrefab;
                    break;
                case 3:
                    l_PropPrefab = i_ShrubPrefab;
                    break;
            }
            
            l_SpawnPoint.y = 0f;
            l_MapProp = Instantiate(l_PropPrefab, l_SpawnPoint, Quaternion.Euler(0f, Random.Range(0, 360), 0f));
            ServerManager.Spawn(l_MapProp);
            i_MapProps.Add(l_MapProp);
            l_PropsSpawned++;
        }
    }
}
