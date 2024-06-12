using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using FishNet;
using FishNet.Object;
using FishNet.Object.Prediction;
using FishNet.Transporting;
using UnityEngine;

public class PredictedProjectile : NetworkBehaviour
{
    private struct MoveData : IReplicateData
    {
        public Vector3 Force;
        private uint tick;

        public MoveData(Vector3 force)
        {
            Force = force;
            tick = default;
        }

        public uint GetTick() => tick;
        public void SetTick(uint value) => tick = value; 
        public void Dispose() { }
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

    //public float MoveSpeed;
    public int Damage;
    public float Speed;
    private bool AtMaxSpeed = false;
    public float MaxSpeed;
    private Rigidbody _rigidbody;
    private bool subscribed = false;
    private MoveData _moveData = new MoveData();

    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
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

    private void Initialize(Vector3 _force)
    {
        _moveData.Force = _force;
        SubscribeToTimeEvents(true);
        StartCoroutine(AutoDestroy());
    }

    private IEnumerator AutoDestroy()
    {
        yield return new WaitForSeconds(3f);
        
        InstanceFinder.ServerManager.Despawn(gameObject);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!base.HasAuthority)
        {
            return;
        }
        
        StopAllCoroutines();

        if (!other.CompareTag("Map"))
        {
            other.GetComponent<ServerSideHealth>().Damage(Damage);
        }
        
        InstanceFinder.ServerManager.Despawn(gameObject);
    }

    public override void OnStartNetwork()
    {
        base.OnStartNetwork();

        if (base.IsServerInitialized && !Owner.IsValid)
        {
            Initialize(transform.forward * Speed);
            Debug.DrawRay(transform.position, transform.forward, Color.yellow, 3f);
        }
        else
        {
            SubscribeToTimeEvents(true);
        }
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
            //Move(_moveData);
        }
        else if (base.IsServerInitialized)
        {
            if (AtMaxSpeed)
            {
                return;
            }
            Move(_moveData);
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

    [Replicate]
    private void Move(MoveData data, ReplicateState state = ReplicateState.Invalid, Channel channel = Channel.Unreliable)
    {
        float currentSpeed = Vector3.SqrMagnitude(_rigidbody.velocity);
        if (currentSpeed < MaxSpeed)
        {
            _rigidbody.AddForce(data.Force, ForceMode.VelocityChange);
        }
        else
        {
            AtMaxSpeed = true;
        }
    }

    [Reconcile]
    private void Reconciliation(ReconcileData data, Channel channel = Channel.Unreliable)
    {
        transform.position = data.Position;
        transform.rotation = data.Rotation;
    }
}
