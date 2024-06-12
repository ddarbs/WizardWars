using System.Collections;
using System.Collections.Generic;
using FishNet.Object;
using UnityEngine;

public class FootstepAudio : NetworkBehaviour
{
    private const float c_FootstepDistance = 0.5f;
    private float i_Distance;
    private Vector3 i_LastPos = Vector3.zero;

    [SerializeField] private AudioSource i_Audio;
    [SerializeField] private AudioClip i_FootstepSFX;

    void Update()
    {
        i_Distance += Vector3.Distance(i_LastPos, transform.position);
        i_LastPos = transform.position;
        if (i_Distance >= c_FootstepDistance)
        {
            i_Audio.pitch = Random.Range(0.95f, 1.05f);
            i_Distance = 0;
            i_Audio.PlayOneShot(i_FootstepSFX);
        }
    }

    public override void OnStartServer()
    {
        base.OnStartServer();

        this.enabled = false;
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        if (base.IsOwner)
        {
            i_Audio.volume /= 2f;
        }
    }
}
