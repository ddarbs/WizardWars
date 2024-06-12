using System.Collections;
using System.Collections.Generic;
using FishNet.Connection;
using FishNet.Managing.Server;
using FishNet.Object;
using UnityEngine;

public class VersionCheck : NetworkBehaviour
{
    private const string c_Version = "0.4.3"; // TODO: update this idiot
    
    public override void OnStartClient()
    {
        base.OnStartClient();

        ServerVersionCheck(base.LocalConnection, c_Version);
    }

    [ServerRpc(RequireOwnership = false)]
    private void ServerVersionCheck(NetworkConnection _conn, string _version)
    {
        if (_version != c_Version)
        {
            ServerManager.Kick(_conn, KickReason.UnexpectedProblem);
        }
    }
}
