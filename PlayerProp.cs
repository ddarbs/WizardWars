using System;
using System.Collections;
using System.Collections.Generic;
using FishNet;
using FishNet.Component.Prediction;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Prediction;
using FishNet.Transporting;
using TMPro;
using UnityEngine;
using Random = UnityEngine.Random;

public class PlayerProp : NetworkBehaviour
{
    private struct MoveData : IReplicateData
    {
        public float Horizontal;
        public float Vertical;
        public float MouseHorizontal;
        private uint tick;
        
        public uint GetTick() => tick;
        public void SetTick(uint value) => tick = value; 
        public void Dispose() { }
        
        public MoveData(float horizontal, float vertical, float mouseHorizontal)
        {
            Horizontal = horizontal;
            Vertical = vertical;
            MouseHorizontal = mouseHorizontal;
            tick = default;
        }
    }

    private struct ReconcileData : IReconcileData
    {
        public Vector3 Position;
        public Quaternion Rotation;
        private uint tick;
        
        public uint GetTick() => tick;
        public void SetTick(uint value) => tick = value; 
        public void Dispose() { }
        
        public ReconcileData(Vector3 position, Quaternion rotation)
        {
            Position = position;
            Rotation = rotation;
            tick = default;
        }
    }
    
    public float TurnSpeed;
    public float MoveSpeed;
    public float MaxSpeed;
    public Camera _camera;
    [SerializeField] private Transform _spellSpawn;
    private Rigidbody _rigidbody;
    private GameObject _model;
    private Transform _cameraTransform;
    private bool subscribed = false;
    private bool Shoot = false;
    private LayerMask i_PropLayerMask;
    private NetworkObject nob;

    [SerializeField] private NetworkObject FireballPrefab;

    [SerializeField] private GameObject i_CrateProp, i_BarrelProp, i_ColumnProp, i_ShrubProp;

    public int p_PropIndex;

    [SerializeField] private GameObject i_SmokeBomb;

    private bool i_Ability;
    private int i_AbilityCharges = 1;
    private bool i_CanHit = false;
    
    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _model = _camera.transform.parent.gameObject;
        _cameraTransform = _camera.transform;
        i_PropLayerMask = LayerMask.GetMask("Prop", "PlayerProp");
        nob = GetComponent<NetworkObject>();
    }

    private void Update()
    {
        if (!subscribed)
        {
            return;
        }
        
        //_camera.transform.Rotate(new Vector3(-Input.GetAxisRaw("Mouse Y") * (TurnSpeed * 0.5625f), 0f, 0f), Space.Self); // 0.5625 is 9/16

        if (Input.GetMouseButtonDown(0))
        {
            Debug.Log("click");
            Shoot = true;
        }

        if (Input.GetKeyDown(KeyCode.Space) && i_AbilityCharges > 0)
        {
            i_Ability = true;
            AbilityUI.LoseAbility();
        }
        
        RaycastHit l_Collider = default;
        bool l_Hit = Physics.Raycast(_spellSpawn.position, _spellSpawn.forward, out l_Collider, 1f, i_PropLayerMask);
        if (l_Hit)
        {
            i_CanHit = true;
            RangeIndicator.CanHit();
        }
        else if (i_CanHit)
        {
            i_CanHit = false;
            RangeIndicator.CanNotHit();
        }
    }

    private void SubscribeToTimeEvents(bool _subcribe)
    {
        if (base.TimeManager == null)
        {
            return;
        }
        if (_subcribe == subscribed)
        {
            return;
        }
        subscribed = _subcribe;

        if (_subcribe)
        {
            base.TimeManager.OnTick += TimeManager_OnTick;
            base.TimeManager.OnPostTick += TimeManager_OnPostTick;
        }
        else
        {
            base.TimeManager.OnTick -= TimeManager_OnTick;
            base.TimeManager.OnPostTick -= TimeManager_OnPostTick;
        }
    }

    public override void OnStartNetwork()
    {
        base.OnStartNetwork();
        
        SubscribeToTimeEvents(true);
    }

    public override void OnStartServer()
    {
        base.OnStartServer();

        if (!base.IsServerInitialized)
        {
            return;
        }

        p_PropIndex = Random.Range(0, 4);
        switch (p_PropIndex)
        {
            case 0://"Barrel":
                i_BarrelProp.SetActive(true);
                i_CrateProp.SetActive(false);
                i_ColumnProp.SetActive(false);
                i_ShrubProp.SetActive(false);
                nob.SetGraphicalObject(i_BarrelProp.transform);
                ObserverShowProp("Barrel");
                break;
            case 1://"Crate":
                i_BarrelProp.SetActive(false);
                i_CrateProp.SetActive(true);
                i_ColumnProp.SetActive(false);
                i_ShrubProp.SetActive(false);
                nob.SetGraphicalObject(i_CrateProp.transform);
                ObserverShowProp("Crate");
                break;
            case 2://"Column":
                i_BarrelProp.SetActive(false);
                i_CrateProp.SetActive(false);
                i_ColumnProp.SetActive(true);
                i_ShrubProp.SetActive(false);
                nob.SetGraphicalObject(i_ColumnProp.transform);
                ObserverShowProp("Column");
                break;
            case 3://"Shrub":
                i_BarrelProp.SetActive(false);
                i_CrateProp.SetActive(false);
                i_ColumnProp.SetActive(false);
                i_ShrubProp.SetActive(true);
                nob.SetGraphicalObject(i_ShrubProp.transform);
                ObserverShowProp("Shrub");
                break;
        }
    }

    [TargetRpc]
    private void TargetPropUI(NetworkConnection _conn, string _prop)
    {
        PropUI.ChangeProp(_prop);
        Debug.Log($"change prop to {_prop}", this);
    }

    [ServerRpc]
    private void ServerForceTargetPropUI()
    {
        Debug.Log($"index {p_PropIndex}", this);
        switch (p_PropIndex)
        {
            case 0://"Barrel":
                TargetPropUI(base.Owner, "Barrel");
                break;
            case 1://"Crate":
                TargetPropUI(base.Owner, "Crate");
                break;
            case 2://"Column":
                TargetPropUI(base.Owner, "Column");
                break;
            case 3://"Shrub":
                TargetPropUI(base.Owner, "Shrub");
                break;
        } 
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        if (!base.IsOwner)
        {
            this.enabled = false;
            return;
        }
        
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        
        i_CrateProp.layer = i_BarrelProp.layer = i_ColumnProp.layer = i_ShrubProp.layer = LayerMask.NameToLayer("MyPlayer");
        _camera.gameObject.SetActive(true);
        ServerForceTargetPropUI();
        AbilityUI.GainPropAbility(true);
    }

    private void OnDestroy()
    {
        SubscribeToTimeEvents(false);
    }

    private void TimeManager_OnTick()
    {
        if (base.IsOwner)
        {
            Reconciliation(default);
            GatherInputs(out MoveData data);
            Move(data);
            if (Shoot)
            {
                Shoot = false;
                ChangeProp();
            }

            if (i_Ability)
            {
                i_AbilityCharges--;
                i_Ability = false;
                AbilitySmokeBomb();
            }
        }
        else if (base.IsServerInitialized)
        {
            Move(default);
        }
    }

    [ServerRpc]
    private void AbilitySmokeBomb()
    {
        if (i_AbilityCharges > 0)
        {
            i_AbilityCharges--;
            ObserverAbilitySmokeBomb();
        }
    }

    [ObserversRpc]
    private void ObserverAbilitySmokeBomb()
    {
        Vector3 _spawn = transform.position;
        _spawn.y = 0.3f;
        Instantiate(i_SmokeBomb, _spawn, Quaternion.identity); // INFO: this auto destroys locally, so handling this locally 
    }

    [ServerRpc]
    private void ChangeProp()
    {
        Debug.Log("shooting");
        RaycastHit l_Collider = default;
        bool l_Hit = Physics.Raycast(_spellSpawn.position, _spellSpawn.forward, out l_Collider, 1f, i_PropLayerMask);
        if (l_Hit)
        {
            switch (l_Collider.collider.tag)
            {
                case "Barrel":
                    i_BarrelProp.SetActive(true);
                    i_CrateProp.SetActive(false);
                    i_ColumnProp.SetActive(false);
                    i_ShrubProp.SetActive(false);
                    nob.SetGraphicalObject(i_BarrelProp.transform);
                    p_PropIndex = 0;
                    break;
                case "Crate":
                    i_BarrelProp.SetActive(false);
                    i_CrateProp.SetActive(true);
                    i_ColumnProp.SetActive(false);
                    i_ShrubProp.SetActive(false);
                    nob.SetGraphicalObject(i_CrateProp.transform);
                    p_PropIndex = 1;
                    break;
                case "Column":
                    i_BarrelProp.SetActive(false);
                    i_CrateProp.SetActive(false);
                    i_ColumnProp.SetActive(true);
                    i_ShrubProp.SetActive(false);
                    nob.SetGraphicalObject(i_ColumnProp.transform);
                    p_PropIndex = 2;
                    break;
                case "Shrub":
                    i_BarrelProp.SetActive(false);
                    i_CrateProp.SetActive(false);
                    i_ColumnProp.SetActive(false);
                    i_ShrubProp.SetActive(true);
                    nob.SetGraphicalObject(i_ShrubProp.transform);
                    p_PropIndex = 3;
                    break;
                default:
                    Debug.LogError(l_Collider.collider.tag, this);
                    break;
            }
            TargetPropUI(base.Owner, l_Collider.collider.tag);
            ObserverShowProp(l_Collider.collider.tag);
        }
    }
    
    [ObserversRpc(ExcludeServer = true, BufferLast = true)]
    private void ObserverShowProp(string _prop)
    {
        Debug.Log("observer show prop");
        switch (_prop)
        {
            case "Barrel":
                i_BarrelProp.SetActive(true);
                i_CrateProp.SetActive(false);
                i_ColumnProp.SetActive(false);
                i_ShrubProp.SetActive(false);
                nob.SetGraphicalObject(i_BarrelProp.transform);
                break;
            case "Crate":
                i_BarrelProp.SetActive(false);
                i_CrateProp.SetActive(true);
                i_ColumnProp.SetActive(false);
                i_ShrubProp.SetActive(false);
                nob.SetGraphicalObject(i_CrateProp.transform);
                break;
            case "Column":
                i_BarrelProp.SetActive(false);
                i_CrateProp.SetActive(false);
                i_ColumnProp.SetActive(true);
                i_ShrubProp.SetActive(false);
                nob.SetGraphicalObject(i_ColumnProp.transform);
                break;
            case "Shrub":
                i_BarrelProp.SetActive(false);
                i_CrateProp.SetActive(false);
                i_ColumnProp.SetActive(false);
                i_ShrubProp.SetActive(true);
                nob.SetGraphicalObject(i_ShrubProp.transform);
                break;
            default:
                Debug.LogError(_prop, this);
                break;
        }
    }
    
    private void TimeManager_OnPostTick()
    {
        CreateReconcile();
    }
    
    public override void CreateReconcile()
    {
        ReconcileData data = new ReconcileData(
            transform.position, 
            transform.rotation
        );
        Reconciliation(data);
    }

    private void GatherInputs(out MoveData data)
    {
        data = default;

        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");
        float mouseHorizontal = Input.GetAxisRaw("Mouse X");
        
        if (horizontal == 0f && vertical == 0f && mouseHorizontal == 0f)
        {
            return;
        }

        data = new MoveData(horizontal, vertical, mouseHorizontal);
    }

    [Replicate]
    private void Move(MoveData data, ReplicateState state = ReplicateState.Invalid, Channel channel = Channel.Unreliable)
    {
        if (data.MouseHorizontal != 0f)
        {
            transform.Rotate(new Vector3(0f, data.MouseHorizontal * TurnSpeed, 0f));
        }
        
        float currentSpeed = Vector3.SqrMagnitude(_rigidbody.velocity);
        if (currentSpeed > MaxSpeed)
        {
            float l_BrakeSpeed = currentSpeed - MaxSpeed;
            Vector3 l_BrakeForce = _rigidbody.velocity.normalized * l_BrakeSpeed;
            _rigidbody.AddRelativeForce(-l_BrakeForce);
        }
        else if (data.Horizontal != 0f || data.Vertical != 0f)
        {
            Vector3 force = new Vector3(data.Horizontal * 0.5f, 0f, data.Vertical) * MoveSpeed;
            _rigidbody.AddRelativeForce(force);
        }
        
        /*if (data.Horizontal != 0f || data.Vertical != 0f)
        {
            Vector3 force = new Vector3(data.Horizontal, 0f, data.Vertical) * MoveSpeed;
            _rigidbody.AddRelativeForce(force);
        }*/
    }

    [Reconcile]
    private void Reconciliation(ReconcileData data, Channel channel = Channel.Unreliable)
    {
        transform.position = data.Position;
        transform.rotation = data.Rotation;
    }
}
