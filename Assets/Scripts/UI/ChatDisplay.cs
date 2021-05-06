using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class ChatDisplay : MonoBehaviour
{
    [SerializeField] private GameObject contentBox;
    [SerializeField] private GameObject chatBox;
    [SerializeField] private GameObject inputBox;
    [SerializeField] private GameObject messagePrefab;
    private string myPlayer;
    private bool isHidden = false;
    private KeyCode hideKey = KeyCode.M;

    private void Start()
    {
        myPlayer = PlayerPrefs.GetString("Name");
        chatBox.SetActive(!isHidden);
        inputBox.SetActive(!isHidden);
    }

    private void Update()
    {
        if(Input.GetKeyDown(hideKey))
        {
            isHidden = !isHidden;
            chatBox.SetActive(!isHidden);
            inputBox.SetActive(!isHidden);
        }
    }

    public void AddMessage(string msg, string player)
    {
        GameObject omsg = Instantiate(messagePrefab, contentBox.transform);
        TextMeshProUGUI tmsg = omsg.GetComponent<TextMeshProUGUI>();
        tmsg.text = msg;
        if (myPlayer.Equals(player)) tmsg.alignment = TextAlignmentOptions.MidlineRight;
        omsg.transform.SetAsLastSibling();
    }
}
