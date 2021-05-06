using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using TMPro;

[RequireComponent(typeof(ChatDisplay))]
public class ChatManager : DistributedEntitieBehaviour
{
    [SerializeField] private TMP_InputField inputText;
    private ChatDisplay display;
    private string myPlayerName;

    private static ChatManager _instance = null;

    private void Start()
    {
        if(_instance != null && _instance != this)
        {
            Destroy(_instance.gameObject);
        }
        _instance = this;
        display = GetComponent<ChatDisplay>();
        inputText.onEndEdit.AddListener(OnSendNewMessage);
        myPlayerName = PlayerPrefs.GetString("Name");
        DontDestroyOnLoad(gameObject);
    }

    private void OnSendNewMessage(string msg)
    {
        string pmsg = myPlayerName + " - " + msg;
        if(isServer)
        {
            Debug.Log("I'm server, running rpc.");
            RpcSendMessage(pmsg, myPlayerName);
        } else
        {
            Debug.Log("I'm client, running command.");
            RunCommand(SendMessageCommandCapsule, pmsg, myPlayerName);
        }
        inputText.text = "";
    }

    private void SendMessageCommandCapsule(string msg, string player)
    {
        CmdSendMessage(msg, player);
    }

    [Command]
    private void CmdSendMessage(string msg, string player)
    {
        RpcSendMessage(msg, player);
        RemoveAuthority();
    }

    [ClientRpc]
    private void RpcSendMessage(string msg, string player)
    {
        display.AddMessage(msg, player);
    }
}
