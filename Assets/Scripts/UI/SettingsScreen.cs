using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.EventSystems;

public class SettingsScreen : MonoBehaviour
{
    [SerializeField] private KeyCode m_exitKey = KeyCode.Escape;
    [SerializeField] private GameObject m_settingsScreen;
    [SerializeField] private TMP_Dropdown m_camDropdown;
    [SerializeField] private TMP_InputField m_fpsInput;
    [SerializeField] private TMP_Dropdown m_micDropdown;
    [SerializeField] private TMP_InputField m_samplingInput;
    [SerializeField] private InputAvailabilityManager inputAvailabilityManager;

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

        m_fpsInput.text = PlayerPrefs.GetInt("CamFPS", int.Parse(m_fpsInput.text)).ToString();
        m_samplingInput.text = PlayerPrefs.GetInt("MicFPS", int.Parse(m_samplingInput.text)).ToString();

        m_fpsInput.onEndEdit.AddListener(OnFrameRateChanged);
        m_samplingInput.onEndEdit.AddListener(OnSamplingRateChanged);
    }

    private void Update()
    {
        if(m_settingsScreen.activeSelf && Input.GetKeyUp(m_exitKey))
        {
            ToggleSettings();
        }
    }

    public void ToggleSettings()
    {
        m_settingsScreen.SetActive(!m_settingsScreen.activeSelf);
        if (!inputAvailabilityManager.UserIsTyping)
        {
            inputAvailabilityManager.UserStartedTyping("on");
        }
        else
        {
            inputAvailabilityManager.UserFinishedTyping("off");
        }
        EventSystem.current.SetSelectedGameObject(null);
    }

    public void OnCamSelected(int index)
    {
        PlayerPrefs.SetString("Cam", m_camDropdown.options[index].text);
    }

    public void OnMicSelected(int index)
    {
        PlayerPrefs.SetString("Mic", m_micDropdown.options[index].text);
    }

    public void OnFrameRateChanged(string value)
    {
        PlayerPrefs.SetInt("CamFPS", int.Parse(value));
    }

    public void OnSamplingRateChanged(string value)
    {
        PlayerPrefs.SetInt("MicFPS", int.Parse(value));
    }
}
