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

    private void Start()
    {
        display = GetComponent<ChatDisplay>();
        inputText.onEndEdit.AddListener(OnSendNewMessage);
    }

    private void OnSendNewMessage(string msg)
    {
        string pmsg = PlayerPrefs.GetString("Name") + " - " + msg;
        if(isServer)
        {
            Debug.Log("I'm server, running rpc.");
            RpcSendMessage(pmsg);
        } else
        {
            Debug.Log("I'm client, running command.");
            RunCommand(SendMessageCommandCapsule, pmsg);
        }
    }

    private void SendMessageCommandCapsule(string msg)
    {
        CmdSendMessage(msg);
    }

    [Command]
    private void CmdSendMessage(string msg)
    {
        RpcSendMessage(msg);
        RemoveAuthority();
    }

    [ClientRpc]
    private void RpcSendMessage(string msg)
    {
        display.AddMessage(msg);
    }
}
