using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using MLAPI;
using MLAPI.Messaging;

[RequireComponent(typeof(ChatDisplay))]
public class ChatManager : NetworkBehaviour
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
        //DontDestroyOnLoad(gameObject);
    }

    private void OnSendNewMessage(string msg)
    {
        string pmsg = myPlayerName + " - " + msg;
        if(IsServer)
        {
            Debug.Log("I'm server, running rpc.");
            SendMessageClientRpc(pmsg,
                GameObject.FindWithTag("LocalPlayer").GetComponent<NetworkObject>().NetworkObjectId);
        } else
        {
            Debug.Log("I'm client, running command.");
            SendMessageServerRpc(pmsg,
                GameObject.FindWithTag("LocalPlayer").GetComponent<NetworkObject>().NetworkObjectId);
        }
        inputText.text = "";
    }

    [ServerRpc(RequireOwnership = false)]
    private void SendMessageServerRpc(string msg, ulong id)
    {
        SendMessageClientRpc(msg, id);
    }

    [ClientRpc]
    private void SendMessageClientRpc(string msg, ulong id)
    {
        display.AddMessage(msg, id);
    }
}
