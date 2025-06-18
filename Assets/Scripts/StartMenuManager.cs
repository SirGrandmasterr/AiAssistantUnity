using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.UIElements;
using Button = UnityEngine.UI.Button;
using Image = UnityEngine.UIElements.Image;

public class StartMenuScript : MonoBehaviour
{
    public Button startEnvironmentButton;

    public Button connectButton;

    public Button loginButton;

    public TMP_InputField Address;

    public TMP_InputField username;

    public TMP_InputField password;
    public TMP_Text infotext;

    public UnityEngine.UI.Image ConnImage;
    public UnityEngine.UI.Image LogImage;
    public WebRtcProvider webRtcProvider;
    [Serializable]
    public struct JWTResponse
    {
        public string token { get; set; }
    }


   
    // Start is called before the first frame update
    void Start()
    {
        infotext.text =
            "1. Enter hostname:port and confirm.\n2. Enter username/password and confirm. Account will be created if it doesn't exist.\n3. Click Enter 3D-Environment";
        if (PlayerPrefs.HasKey("Address"))
        {
            Address.text = PlayerPrefs.GetString("Address");
        }
        if (PlayerPrefs.HasKey("username"))
        {
            username.text = PlayerPrefs.GetString("username");
        }
        if (PlayerPrefs.HasKey("password"))
        {
            password.text = PlayerPrefs.GetString("password");
        }

        password.contentType = TMP_InputField.ContentType.Password;

        startEnvironmentButton.enabled = false;
        loginButton.enabled = false;
        connectButton.onClick.AddListener(OnConnectButtonPress);
        loginButton.onClick.AddListener(OnLoginButtonPress);
        startEnvironmentButton.onClick.AddListener(OnStartEnvironmentButtonPress);
    }

    // Update is called once per frame
    void OnConnectButtonPress()
    {
        StartCoroutine(TestConnection("http://" + Address.text + "/ping"));
    }

    void OnLoginButtonPress()
    {
        
        StartCoroutine(LoginOrCreate());
    }

    void OnStartEnvironmentButtonPress()
    {
        if (webRtcProvider.GetConnectionState() == "Connected")
        {
            SceneManager.LoadScene("ScenarioSelect");
        }
    }
    
    IEnumerator TestConnection(string uri)
    { 
        using (UnityWebRequest webRequest = UnityWebRequest.Get(uri))
        {
            // Request and wait for the desired page.
            yield return webRequest.SendWebRequest();

            string[] pages = uri.Split('/');
            int page = pages.Length - 1;

            switch (webRequest.result)
            {
                case UnityWebRequest.Result.ConnectionError:
                    loginButton.enabled = false;
                    break;
                case UnityWebRequest.Result.DataProcessingError:
                    loginButton.enabled = false;
                    break;
                case UnityWebRequest.Result.ProtocolError:
                    loginButton.enabled = false;
                    break;
                case UnityWebRequest.Result.Success:
                    PlayerPrefs.SetString("Address", Address.text);
                    ConnImage.color = Color.green;
                    loginButton.enabled = true;
                    break;
            }
        }
    }
    
    IEnumerator LoginOrCreate()
    {
        LogImage.color = Color.yellow;
        Debug.Log(username.text);
        var formData = new List<IMultipartFormSection>();
        formData.Add(new MultipartFormDataSection("user="+username.text+"&pass="+password.text));
        using (UnityWebRequest webRequest = UnityWebRequest.Post(PlayerPrefs.GetString("Address") + "/login?user="+username.text+"&pass="+password.text, formData))
        {
            // Request and wait for the desired page.
            yield return webRequest.SendWebRequest();
            Debug.Log(username.text);
            switch (webRequest.result)
            {
                case UnityWebRequest.Result.ConnectionError:
                    break;
                case UnityWebRequest.Result.DataProcessingError:
                    break;
                case UnityWebRequest.Result.ProtocolError:
                    LogImage.color = Color.yellow;
                    infotext.text = "Login unsuccessful. Trying to create Account.";
                    StartCoroutine(Create());
                    break;
                case UnityWebRequest.Result.Success:
                    JWTResponse resp = JsonUtility.FromJson<JWTResponse>(webRequest.downloadHandler.text);
                    var str = webRequest.downloadHandler.text;
                    str = str.Substring(10);
                    str = str.Substring(0, str.Length-2);
                    Debug.Log(str);
                    PlayerPrefs.SetString("playerJwt", str);
                    PlayerPrefs.SetString("username", username.text);
                    PlayerPrefs.SetString("password", password.text);
                    infotext.text = "Login successful. Proceed.";
                    startEnvironmentButton.enabled = true;
                    LogImage.color = Color.green;
                    break;
            }
        }
    }
    
    IEnumerator Create()
    {
        LogImage.color = Color.yellow;
        Debug.Log(username.text);
        var formData = new List<IMultipartFormSection>();
        formData.Add(new MultipartFormDataSection("user="+username.text+"&pass="+password.text));
        using (UnityWebRequest webRequest = UnityWebRequest.Post(PlayerPrefs.GetString("Address") + "/create?user="+username.text+"&pass="+password.text, formData))
        {
            // Request and wait for the desired page.
            yield return webRequest.SendWebRequest();
            switch (webRequest.result)
            {
                case UnityWebRequest.Result.ConnectionError:
                    Debug.LogError(UnityWebRequest.Result.ConnectionError);
                    break;
                case UnityWebRequest.Result.DataProcessingError:
                    Debug.LogError(UnityWebRequest.Result.DataProcessingError);
                    break;
                case UnityWebRequest.Result.ProtocolError:
                    Debug.LogError(UnityWebRequest.Result.ProtocolError);
                    LogImage.color = Color.red;
                    infotext.text = "Account could not be created. Please try a different username.";
                    
                    break;
                case UnityWebRequest.Result.Success:
                    
                    StartCoroutine(LoginOrCreate());
                    break;
            }
        }
    }
}
