using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

public class MatchStarter : NetworkBehaviour, InteractableObject
{
    [SerializeField] private KeyCode interactionKey = KeyCode.Z;
    [SerializeField] private NetworkManager netManager;
    [SerializeField] private int minNumberOfPlayers;
    [SerializeField] private int maxNumberOfPlayers;

    int currNumberOfPlayers = 0;

    private bool readyConfirmationPending = true;
    private bool gameIsStarting = false;
    [SerializeField] private float matchCountdown = 60;
    [SerializeField] private string gameScene;
    private NetworkIdentity netIdentity;

    private void Start()
    {
        netIdentity = GetComponent<NetworkIdentity>();
    }

    public KeyCode GetKey()
    {
        return interactionKey;
    }

    public void Interact()
    {
        if (readyConfirmationPending)
        {
            readyConfirmationPending = false;
            Messenger.Broadcast(LobbyEvent.WAITING_FOR_MATCH);
            CmdUpdateNumberOfReadyPlayers();
        }
    }

    [Command]
    private void CmdUpdateNumberOfReadyPlayers()
    {
        if (currNumberOfPlayers < maxNumberOfPlayers)
        {
            currNumberOfPlayers += 1;
            RpcUpdateNumberOfReadyPlayers();
            if (gameIsStarting) RpcGetReadyForMatch();
            Debug.Log("Server Ready Players: " + currNumberOfPlayers.ToString());
        }
    }

    [ClientRpc]
    private void RpcUpdateNumberOfReadyPlayers()
    {
        currNumberOfPlayers += 1;
        Debug.Log("Ready players: " + currNumberOfPlayers);
    }

    [ClientRpc]
    private void RpcGetReadyForMatch()
    {
        gameIsStarting = true;
    }

    [ClientRpc]
    private void RpcUpdateCountdown(float countdown)
    {
        if(gameIsStarting)
        {
            matchCountdown = countdown;
            Messenger<int>.Broadcast(LobbyEvent.MATCH_COUNTDOWN_UPDATE, (int) countdown);
        }
    }

    private void Update()
    {
        if(isServer)
        {
            if(currNumberOfPlayers >= minNumberOfPlayers && !gameIsStarting)
            {
                gameIsStarting = true;
                RpcGetReadyForMatch();
            }

            if (gameIsStarting)
            {
                if (matchCountdown > 0.0f)
                {
                    RpcUpdateCountdown(matchCountdown - Time.deltaTime);
                }
                else
                {
                    netManager.ServerChangeScene(gameScene);
                }
            }
        } else
        {
            if(!netIdentity.hasAuthority)
            {
                GameObject player = GameObject.FindGameObjectWithTag("LocalPlayer");
                NetworkIdentity playerId = player.GetComponent<NetworkIdentity>();
                player.GetComponent<Player>().CmdSetAuth(netId, playerId);
            }
        }
    }
}
