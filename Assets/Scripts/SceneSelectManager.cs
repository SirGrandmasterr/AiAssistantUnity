using System.Collections;
using System.Collections.Generic;
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

     
    private WebRtcProvider _webRtcProvider;
    // Start is called before the first frame update
    void Start()
    {
        _webRtcProvider = WebRtcProvider.Instance;
        _address = PlayerPrefs.GetString("Address");
        StartCoroutine(TestConnection("http://" + _address + "/ping"));
        StartCoroutine(ttsConnectionState());
        


        scenario1Button.onClick.AddListener(SetScenario1);
        scenario2Button.onClick.AddListener(SetScenario2);
        scenario3Button.onClick.AddListener(SetScenario3);
        scenario4Button.onClick.AddListener(SetScenario4);
        
        
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
        PlayerPrefs.SetString("Scenario", "scenario1");
        PlayerPrefs.SetInt("scenarioInt", 1);
        PlayerPrefs.SetString("selectedBasePrompt", "scenario1");
        SceneManager.LoadScene("Gallery MWS_DEMO");
    }
    void SetScenario2()
    {
        PlayerPrefs.SetString("Scenario", "scenario2");
        PlayerPrefs.SetInt("scenarioInt", 2);
        PlayerPrefs.SetString("selectedBasePrompt", "scenario2");
        SceneManager.LoadScene("Gallery MWS_DEMO");
    }
    void SetScenario3()
    {
        PlayerPrefs.SetString("Scenario", "scenario3");
        PlayerPrefs.SetInt("scenarioInt", 3);
        PlayerPrefs.SetString("selectedBasePrompt", "scenario3");
        SceneManager.LoadScene("Gallery MWS_DEMO");
    }
    void SetScenario4()
    {
        PlayerPrefs.SetString("Scenario", "scenario4");
        PlayerPrefs.SetInt("scenarioInt", 4);
        PlayerPrefs.SetString("selectedBasePrompt", "scenario4");
        SceneManager.LoadScene("Gallery MWS_DEMO");
    }
    
   
}
