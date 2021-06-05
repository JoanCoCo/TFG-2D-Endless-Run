using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class SettingsScreen : MonoBehaviour
{
    [SerializeField] private KeyCode m_exitKey = KeyCode.Escape;
    [SerializeField] private GameObject m_settingsScreen;
    [SerializeField] private TMP_Dropdown m_camDropdown;
    [SerializeField] private TMP_Dropdown m_micDropdown;

    private void Start()
    {
        m_settingsScreen.SetActive(false);
        List<string> options = new List<string>();

        string targetCam = PlayerPrefs.GetString("Cam");
        string targetMic = PlayerPrefs.GetString("Mic");
        int i = 0;
        foreach (var mic in Microphone.devices)
        {
            options.Add(mic);
            if (mic.Equals(targetMic)) i = options.Count - 1;
        }
        m_micDropdown.AddOptions(options);
        m_micDropdown.value = i;
        options.Clear();
        i = 0;
        foreach(var cam in WebCamTexture.devices)
        {
            options.Add(cam.name);
            if (cam.name.Equals(targetCam)) i = options.Count - 1;
        }
        m_camDropdown.AddOptions(options);
        m_camDropdown.value = i;

        m_micDropdown.onValueChanged.AddListener(OnMicSelected);
        m_camDropdown.onValueChanged.AddListener(OnCamSelected);
    }

    private void Update()
    {
        if(m_settingsScreen.activeSelf && Input.GetKeyDown(m_exitKey))
        {
            ToggleSettings();
        }
    }

    public void ToggleSettings()
    {
        m_settingsScreen.SetActive(!m_settingsScreen.activeSelf);
    }

    public void OnCamSelected(int index)
    {
        PlayerPrefs.SetString("Cam", m_camDropdown.options[index].text);
    }

    public void OnMicSelected(int index)
    {
        PlayerPrefs.SetString("Mic", m_micDropdown.options[index].text);
    }
}
