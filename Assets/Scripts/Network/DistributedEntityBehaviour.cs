using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MLAPI;

public class DistributedEntityBehaviour : NetworkBehaviour
{
    public delegate void CommandNoArguments();
    public delegate void CommandOneArgument<T>(T arg1);
    public delegate void CommandTwoArguments<T, D>(T arg1, D arg2);

    public delegate void Command(params object[] args);

    public void RemoveAuthority()
    {
        GetComponent<NetworkObject>().RemoveOwnership();
    }

    public void RemoveOwnership() { RemoveAuthority(); }

    public Coroutine GetAuthority() => StartCoroutine(GetAuthorityCoroutine());

    IEnumerator GetAuthorityCoroutine()
    {
        if (!IsServer)
        {
            GameObject player = GameObject.FindGameObjectWithTag("LocalPlayer");
            NetworkObject netIdentity = GetComponent<NetworkObject>();
            while (!netIdentity.IsOwner)
            {
                while (player == null)
                {
                    Debug.Log("Looking for the local player...");
                    player = GameObject.FindGameObjectWithTag("LocalPlayer");
                    yield return new WaitForSeconds(0.01f);
                }
                Debug.Log("Local player found, asking for authority.");
                NetworkObject playerId = player.GetComponent<NetworkObject>();
                player.GetComponent<Player>().SetAuthServerRpc(netIdentity.NetworkObjectId);
                yield return new WaitForSeconds(0.01f);
            }
            Debug.Log("Authority received.");
            //myPlayerId = player.GetComponent<NetworkObject>().netId.Value;
        }
    }

    IEnumerator WaitForAuthority(CommandNoArguments cmd)
    {
        if (!IsServer)
        {
            GetAuthority();
            NetworkObject netIdentity = GetComponent<NetworkObject>();
            while (!netIdentity.IsOwner)
            {
                yield return new WaitForSeconds(0.01f);
            }

        }
        cmd();
    }

    IEnumerator WaitForAuthority<T>(CommandOneArgument<T> cmd, T arg)
    {
        if (!IsServer)
        {
            GetAuthority();
            NetworkObject netIdentity = GetComponent<NetworkObject>();
            while (!netIdentity.IsOwner)
            {
                yield return new WaitForSeconds(0.01f);
            }
        }
        cmd(arg);
    }

    IEnumerator WaitForAuthority<T, D>(CommandTwoArguments<T, D> cmd, T arg1, D arg2)
    {
        if (!IsServer)
        {
            GetAuthority();
            NetworkObject netIdentity = GetComponent<NetworkObject>();
            while (!netIdentity.IsOwner)
            {
                yield return new WaitForSeconds(0.01f);
            }
        }
        cmd(arg1, arg2);
    }

    public Coroutine RunCommand(CommandNoArguments cmd) => StartCoroutine(WaitForAuthority(cmd));

    public Coroutine RunCommand<T>(CommandOneArgument<T> cmd, T arg) => StartCoroutine(WaitForAuthority(cmd, arg));

    public Coroutine RunCommand<T, D>(CommandTwoArguments<T, D> cmd, T arg1, D arg2) => StartCoroutine(WaitForAuthority(cmd, arg1, arg2));

}
