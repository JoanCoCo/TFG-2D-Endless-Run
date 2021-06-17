using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MLAPI;
using TMPro;

[RequireComponent(typeof(TextMeshProUGUI))]
public class PeerLabeler : NetworkBehaviour
{
    private TextMeshProUGUI m_text;
    private bool m_isHost = false;

    void Start()
    {
        m_text = GetComponent<TextMeshProUGUI>();
        UpdateText();
    }

    void Update()
    {
        bool peerType = IsServer;
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
