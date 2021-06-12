using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using MLAPI;

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
        Messenger.AddListener(GameEvent.FINISHED_SCREEN_IS_OUT, OnFinishedScreen);
    }

    private void OnFinishedScreen()
    {
        Destroy(gameObject);
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

    public void AddMessage(string msg, ulong id)
    {
        GameObject omsg = Instantiate(messagePrefab, contentBox.transform);
        TextMeshProUGUI tmsg = omsg.GetComponent<TextMeshProUGUI>();
        tmsg.text = msg;
        ulong myPlayerNetId = GameObject.FindWithTag("LocalPlayer").GetComponent<NetworkObject>().NetworkObjectId;
        if (myPlayerNetId == id) tmsg.alignment = TextAlignmentOptions.MidlineRight;
        omsg.transform.SetAsLastSibling();
    }

    public void ChangeState()
    {
        isHidden = !isHidden;
        UpdateDisplay();
    }

    private void OnDestroy()
    {
        Messenger.RemoveListener(GameEvent.FINISHED_SCREEN_IS_OUT, OnFinishedScreen);
    }
}
