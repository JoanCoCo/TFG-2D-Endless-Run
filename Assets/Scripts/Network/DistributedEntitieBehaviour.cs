using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class DistributedEntitieBehaviour : NetworkBehaviour
{
    public delegate void CommandNoArguments();
    public delegate void CommandOneArgument<T>(T arg1);

    public void RemoveAuthority()
    {
        var owner = GetComponent<NetworkIdentity>().clientAuthorityOwner;
        if (owner != null) GetComponent<NetworkIdentity>().RemoveClientAuthority(owner);
    }

    public void RemoveOwnership() { RemoveAuthority(); }

    public Coroutine GetAuthority() => StartCoroutine(GetAuthorityCoroutine());

    IEnumerator GetAuthorityCoroutine()
    {
        if (!isServer)
        {
            GameObject player = GameObject.FindGameObjectWithTag("LocalPlayer");
            NetworkIdentity netIdentity = GetComponent<NetworkIdentity>();
            while (!netIdentity.hasAuthority)
            {
                while (player == null)
                {
                    Debug.Log("Looking for the local player...");
                    player = GameObject.FindGameObjectWithTag("LocalPlayer");
                    yield return new WaitForSeconds(0.01f);
                }
                Debug.Log("Local player found, asking for authority.");
                NetworkIdentity playerId = player.GetComponent<NetworkIdentity>();
                player.GetComponent<Player>().CmdSetAuth(netId, playerId);
                yield return new WaitForSeconds(0.01f);
            }
            Debug.Log("Authority received.");
            //myPlayerId = player.GetComponent<NetworkIdentity>().netId.Value;
        }
    }

    IEnumerator WaitForAuthority(CommandNoArguments cmd)
    {
        if (!isServer)
        {
            GetAuthority();
            NetworkIdentity netIdentity = GetComponent<NetworkIdentity>();
            while (!netIdentity.hasAuthority)
            {
                yield return new WaitForSeconds(0.01f);
            }
            cmd();
        }
    }

    IEnumerator WaitForAuthority<T>(CommandOneArgument<T> cmd, T arg)
    { 
        if (!isServer)
        {
            GetAuthority();
            NetworkIdentity netIdentity = GetComponent<NetworkIdentity>();
            while (!netIdentity.hasAuthority)
            {
                yield return new WaitForSeconds(0.01f);
            }
            cmd(arg);
        }
    }

    public Coroutine RunCommand(CommandNoArguments cmd) => StartCoroutine(WaitForAuthority(cmd));

    public Coroutine RunCommand<T>(CommandOneArgument<T> cmd, T arg) => StartCoroutine(WaitForAuthority(cmd, arg));
}
