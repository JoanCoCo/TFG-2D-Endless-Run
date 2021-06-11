using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using TMPro;

[RequireComponent(typeof(TextMeshProUGUI))]
public class PeerLabeler : MonoBehaviour
{
    private TextMeshProUGUI m_text;
    private bool m_isHost = false;
    private NetworkIdentity networkIdentity;

    // Start is called before the first frame update
    void Start()
    {
        m_text = GetComponent<TextMeshProUGUI>();
        networkIdentity = transform.parent.GetComponent<NetworkIdentity>();
        UpdateText();
    }

    // Update is called once per frame
    void Update()
    {
        bool peerType = networkIdentity.isServer;
        if (peerType != m_isHost)
        {
            m_isHost = peerType;
            UpdateText();
        }
    }

    private void UpdateText()
    {
        string peerType = (m_isHost) ? "Host" : "Client";
        m_text.text = "Playing as " + PlayerPrefs.GetString("Name") + " (" + peerType + ")";
    }
}
