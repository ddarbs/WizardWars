using System;
using System.Collections;
using System.Collections.Generic;
using FishNet;
using FishNet.Component.Animating;
using FishNet.Component.Prediction;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Prediction;
using FishNet.Transporting;
using TMPro;
using UnityEngine;
using Random = UnityEngine.Random;

public class PlayerSeeker : NetworkBehaviour
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
    [SerializeField] private GameObject _model, _staff;
    [SerializeField] private Transform _spellSpawn;
    [SerializeField] private GameObject _seekerLight;
    private Rigidbody _rigidbody;
    private Transform _cameraTransform;
    private bool subscribed = false;
    private bool Shoot = false;
    private LayerMask i_PropLayerMask;
    private NetworkObject nob;
    private NetworkAnimator i_NetworkAnimator;
    
    private Animator i_Animator;
    private static readonly int Forward = Animator.StringToHash("Forward");
    private static readonly int Backward = Animator.StringToHash("Backward");
    private static readonly int Right = Animator.StringToHash("Right");
    private static readonly int Left = Animator.StringToHash("Left");
    private static readonly int Idle = Animator.StringToHash("Idle");
    private static readonly int Attack = Animator.StringToHash("Attack");

    [SerializeField] private AudioSource i_Audio;
    [SerializeField] private AudioClip i_HitSFX, i_DeathSFX, i_AbilitySFX;

    private bool i_CanShoot = false;
    private bool i_CanHit = false;
    
    private bool i_Ability;
    private int i_AbilityCharges = 1;
    
    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _cameraTransform = _camera.transform;
        i_PropLayerMask = LayerMask.GetMask("Prop", "PlayerProp");
        nob = GetComponent<NetworkObject>();
        i_Animator = GetComponent<Animator>();
        i_NetworkAnimator = GetComponent<NetworkAnimator>();
    }

    private void Update()
    {
        if (!subscribed)
        {
            return;
        }
        
        if (base.IsServerInitialized)
        {
            Vector3 l_LocalVelocity = transform.InverseTransformDirection(_rigidbody.velocity);
            if (Shoot)
            {
                Shoot = false;
                i_Animator.SetTrigger(Attack);
                i_NetworkAnimator.SetTrigger(Attack);
            }
            else if (l_LocalVelocity.z > 0.15f)
            {
                i_Animator.SetTrigger(Forward);
                i_NetworkAnimator.SetTrigger(Forward);
            }
            else if (l_LocalVelocity.z < -0.15f)
            {
                i_Animator.SetTrigger(Backward);
                i_NetworkAnimator.SetTrigger(Backward);
            }
            else if (l_LocalVelocity.x > 0.15f)
            {
                i_Animator.SetTrigger(Right);
                i_NetworkAnimator.SetTrigger(Right);
            }
            else if (l_LocalVelocity.x < -0.15f)
            {
                i_Animator.SetTrigger(Left);
                i_NetworkAnimator.SetTrigger(Left);
            }
            else
            {
                i_Animator.SetTrigger(Idle);
                i_NetworkAnimator.SetTrigger(Idle);
            }
        }
        
        if (!base.Owner.IsLocalClient)
        {
            return;
        }
        
        //_camera.transform.Rotate(new Vector3(-Input.GetAxisRaw("Mouse Y") * (TurnSpeed * 0.5625f), 0f, 0f), Space.Self); // 0.5625 is 9/16

        if (Input.GetMouseButtonDown(0) && i_CanShoot)
        {
            i_CanShoot = false;
            Debug.Log("click");
            Shoot = true;
        }
        
        if (Input.GetKeyDown(KeyCode.Space) && i_AbilityCharges > 0)
        {
            i_Ability = true;
            AbilityUI.LoseAbility();
        }
        
        RaycastHit l_Collider = default;
        bool l_Hit = Physics.Raycast(_spellSpawn.position, _spellSpawn.forward, out l_Collider, 0.4f, i_PropLayerMask);
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

    private IEnumerator MeleeCooldown()
    {
        yield return new WaitForSeconds(1f);
        i_CanShoot = true;
    }

    private IEnumerator MeleeDelay()
    {
        yield return new WaitForSeconds(0.5f);
        
        RaycastHit l_Collider = default;
        bool l_Hit = Physics.Raycast(_spellSpawn.position, _spellSpawn.forward, out l_Collider, 0.4f, i_PropLayerMask);

        if (l_Hit)
        {
            ObserverPlaySFX(0);
            if (l_Collider.transform.GetComponentInParent<ServerSideHealth>() != null) // TODO: need a better way of doing this, probably tags after fixing prop changing
            {
                if (l_Collider.transform.GetComponentInParent<ServerSideHealth>().Damage(1))
                {
                    ObserverPlaySFX(1);
                }
                Debug.Log($"hit player {l_Collider.transform.name} for 1 dmg");
            }
            else
            {
                Debug.Log($"hit non-player {l_Collider.transform.name}");
            }
        }
    }
    
    [ObserversRpc]
    private void ObserverPlaySFX(int _sfxIndex)
    {
        switch (_sfxIndex)
        {
            case 0:
                i_Audio.PlayOneShot(i_HitSFX);
                break;
            case 1:
                i_Audio.PlayOneShot(i_DeathSFX);
                break;
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
        
        //_seekerLight.SetActive(false);
        _model.layer = LayerMask.NameToLayer("MyPlayer");//_staff.layer = LayerMask.NameToLayer("MyPlayer");

        Transform[] l_Children = _model.GetComponentsInChildren<Transform>();
        foreach (Transform _child in l_Children)
        {
            _child.gameObject.layer = LayerMask.NameToLayer("MyPlayer");
        }
        
        _camera.gameObject.SetActive(true);
        AbilityUI.GainSeekerAbility(true);

        i_CanShoot = true;
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
                ServerMelee();
                StartCoroutine(MeleeCooldown());
            }
            
            if (i_Ability)
            {
                i_AbilityCharges--;
                i_Ability = false;
                AbilitySprint();
            }
        }
        else if (base.IsServerInitialized)
        {
            Move(default);
        }
    }

    [ServerRpc]
    private void ServerMelee()
    {
        Shoot = true; // triggers attack animation

        StartCoroutine(MeleeDelay());
    }
    
    [ServerRpc]
    private void AbilitySprint()
    {
        if (i_AbilityCharges > 0)
        {
            i_AbilityCharges--;
            StartCoroutine(AbilitySprintThread());
            Observer_AbilitySprintSFX();
        }
    }

    [ObserversRpc]
    private void Observer_AbilitySprintSFX()
    {
        i_Audio.PlayOneShot(i_AbilitySFX, 0.75f);
    }

    private IEnumerator AbilitySprintThread()
    {
        MaxSpeed *= 2f;
        yield return new WaitForSeconds(5f);
        MaxSpeed /= 2f;
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
            Vector3 force = new Vector3(data.Horizontal * 0.5f, 0f, data.Vertical) * MoveSpeed;
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
