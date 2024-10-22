using System.Threading.Tasks;
using NativeWebSocket;
using UnityEngine;

public class WebSocketClient : MonoBehaviour
{
    [SerializeField] public int audioPackages;
    [SerializeField] public string textRequest = "";
    public SoundGetter _soundGetter;
    private WebSocket websocket;

    // Start is called before the first frame update
    private async void Start()
    {
        websocket = new WebSocket("ws://localhost:8000");

        websocket.OnOpen += () => { Debug.Log("TTS Connection open!"); };

        websocket.OnError += e => { Debug.Log("Error! " + e); };

        websocket.OnClose += e => { Debug.Log("TTS Connection closed!"); };

        websocket.OnMessage += bytes =>
        {
            _soundGetter.AddToQueue();
        };

        // waiting for messages
        await websocket.Connect();
    }

    private void Update()
    {
#if !UNITY_WEBGL || UNITY_EDITOR
        websocket.DispatchMessageQueue();
#endif
    }

    private async void OnApplicationQuit()
    {
        await websocket.Close();
    }

    private async Task SendWebSocketMessage(string text)
    {
        if (websocket.State == WebSocketState.Open)
            // Sending plain text
            await websocket.SendText(text);
    }

    public async Task ListenTo(string text)
    {
        print("WebsocketClient listened and sends: " + text);
        await SendWebSocketMessage(text);
    }
}