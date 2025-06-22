using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using Button = UnityEngine.UI.Button;
public class SceneSelectManager : MonoBehaviour
{
    private string _address;
    public UnityEngine.UI.Image backendImage;
    public UnityEngine.UI.Image ttsImage;
    public Button scenario1Button;
    public Button scenario2Button;
    public Button scenario3Button;
    public Button scenario4Button;
    public Button setRecordingButton;
    public TMP_InputField recordingInput;
    public TMP_Text recordingsText;
    

     
    private WebRtcProvider _webRtcProvider;
    // Start is called before the first frame update
    void Start()
    {
        _webRtcProvider = WebRtcProvider.Instance;
        _address = PlayerPrefs.GetString("Address");
        StartCoroutine(TestConnection("http://" + _address + "/ping"));
        StartCoroutine(ttsConnectionState());
        PlayerPrefs.SetInt("maxRecordings", 2);
        recordingsText.text = "Recordings: " + PlayerPrefs.GetInt("maxRecordings").ToString();

        scenario1Button.onClick.AddListener(SetScenario1);
        scenario2Button.onClick.AddListener(SetScenario2);
        scenario3Button.onClick.AddListener(SetScenario3);
        scenario4Button.onClick.AddListener(SetScenario4);
        
        scenario4Button.onClick.AddListener(SetMaxRecordings);
        
    }

    public void SetMaxRecordings()
    {
        // Ensure the input field is not null before proceeding
        if (recordingInput == null)
        {
            Debug.LogError("Recording Input Field is not assigned!");
            return;
        }

        string inputText = recordingInput.text;
        int parsedValue;

        // Try to parse the input text into an integer
        if (int.TryParse(inputText, out parsedValue))
        {
            // Check if the parsed value is within the valid range (1 to 10)
            if (parsedValue >= 1 && parsedValue <= 10)
            {
                // If valid, set the PlayerPref
                PlayerPrefs.SetInt("maxRecordings", parsedValue);
                recordingsText.text = "Recordings: " + PlayerPrefs.GetInt("maxRecordings").ToString();
                // Save changes to disk immediately (good practice for PlayerPrefs)
                PlayerPrefs.Save();
                Debug.Log($"PlayerPref 'maxRecordings' set to: {parsedValue}");
            }
            else
            {
                // Value is an integer but out of range
                Debug.LogWarning($"Entered value '{inputText}' is out of the valid range (1-10). PlayerPref not set.");
            }
        }
        else
        {
            // Input text is not a valid integer
            Debug.LogWarning($"Entered text '{inputText}' is not a valid integer. PlayerPref not set.");
        }
    }
    IEnumerator TestConnection(string uri)
    {
        using UnityWebRequest webRequest = UnityWebRequest.Get(uri);
        // Request and wait for the desired page.
        yield return webRequest.SendWebRequest();

        string[] pages = uri.Split('/');
        int page = pages.Length - 1;

        switch (webRequest.result)
        {
            case UnityWebRequest.Result.ConnectionError:
                scenario1Button.enabled = false;
                scenario2Button.enabled = false;
                scenario3Button.enabled = false;
                scenario4Button.enabled = false;
                break;
            case UnityWebRequest.Result.DataProcessingError:
                scenario1Button.enabled = false;
                scenario2Button.enabled = false;
                scenario3Button.enabled = false;
                scenario4Button.enabled = false;
                break;
            case UnityWebRequest.Result.ProtocolError:
                scenario1Button.enabled = false;
                scenario2Button.enabled = false;
                scenario3Button.enabled = false;
                scenario4Button.enabled = false;
                break;
            case UnityWebRequest.Result.Success:
                PlayerPrefs.SetString("Address", _address);
                backendImage.color = Color.green;
                scenario1Button.enabled = true;
                scenario2Button.enabled = true;
                scenario3Button.enabled = true;
                scenario4Button.enabled = true;
                break;
        }
    }

    // Update is called once per frame
    private IEnumerator ttsConnectionState()
    {
        while (true)
        {
            if (_webRtcProvider.GetConnectionState() == "Connected")
            {
                Debug.Log("Setting true?");
                ttsImage.color = Color.green;
                break;
            }

            WaitForSeconds wait = new WaitForSeconds(0.5f);
        }
        yield return null;
    }

    void SetScenario1()
    {
        PlayerPrefs.SetString("Scenario", "sad");
        PlayerPrefs.SetInt("scenarioInt", 1);
        PlayerPrefs.SetString("selectedBasePrompt", "sad");
        SceneManager.LoadScene("Gallery MWS_DEMO");
    }
    void SetScenario2()
    {
        PlayerPrefs.SetString("Scenario", "angry");
        PlayerPrefs.SetInt("scenarioInt", 2);
        PlayerPrefs.SetString("selectedBasePrompt", "angry");
        SceneManager.LoadScene("Gallery MWS_DEMO");
    }
    void SetScenario3()
    {
        PlayerPrefs.SetString("Scenario", "joyful");
        PlayerPrefs.SetInt("scenarioInt", 3);
        PlayerPrefs.SetString("selectedBasePrompt", "joyful");
        SceneManager.LoadScene("Gallery MWS_DEMO");
    }
    void SetScenario4()
    {
        PlayerPrefs.SetString("Scenario", "surprised");
        PlayerPrefs.SetInt("scenarioInt", 4);
        PlayerPrefs.SetString("selectedBasePrompt", "surprised");
        SceneManager.LoadScene("Gallery MWS_DEMO");
    }
    
   
}
