using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using TMPro;
using UnityEngine.UI;

public class ChatDisplay : MonoBehaviour
{
    [SerializeField] private GameObject contentBox;
    [SerializeField] private GameObject chatBox;
    [SerializeField] private GameObject inputBox;
    [SerializeField] private GameObject messagePrefab;
    [SerializeField] private GameObject chatIcon;
    private string myPlayer;
    [SerializeField] private bool isHidden = false;
    private KeyCode hideKey = KeyCode.M;

    private InputAvailabilityManager inputAvailabilityManager;

    private void Start()
    {
        myPlayer = PlayerPrefs.GetString("Name");
        UpdateDisplay();
        inputAvailabilityManager = GameObject.FindWithTag("InputAvailabilityManager").GetComponent<InputAvailabilityManager>();
    }

    private void Update()
    {
        if((Input.GetKeyDown(hideKey) || Input.GetKeyDown(KeyCode.Escape) && !isHidden) && (inputAvailabilityManager == null || !inputAvailabilityManager.UserIsTyping))
        {
            ChangeState();
        }
    }

    private void UpdateDisplay()
    {
        chatBox.SetActive(!isHidden);
        inputBox.SetActive(!isHidden);
        chatIcon.SetActive(isHidden);
    }

    public void AddMessage(string msg, uint id)
    {
        GameObject omsg = Instantiate(messagePrefab, contentBox.transform);
        TextMeshProUGUI tmsg = omsg.GetComponent<TextMeshProUGUI>();
        tmsg.text = msg;
        uint myPlayerNetId = GameObject.FindWithTag("LocalPlayer").GetComponent<NetworkIdentity>().netId.Value;
        if (myPlayerNetId == id) tmsg.alignment = TextAlignmentOptions.MidlineRight;
        omsg.transform.SetAsLastSibling();
    }

    public void ChangeState()
    {
        isHidden = !isHidden;
        UpdateDisplay();
    }
}
