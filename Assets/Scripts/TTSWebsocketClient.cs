using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using NativeWebSocket;
using UnityEngine;
using Debug = UnityEngine.Debug;

public class WebSocketClient : MonoBehaviour
{
    [SerializeField] public int audioPackages;
    [SerializeField] public string textRequest = "";
    public SoundGetter _soundGetter;
    private WebSocket websocket;
    private Stopwatch sw;
    private Queue<long> timequeue;

    // Start is called before the first frame update
    private async void Start()
    {
        sw = new Stopwatch();
        timequeue = new Queue<long>();
        websocket = new WebSocket("ws://localhost:8000");

        websocket.OnOpen += () => { Debug.Log("TTS Connection open!"); };

        websocket.OnError += e => { Debug.Log("Error! " + e); };

        websocket.OnClose += e => { Debug.Log("TTS Connection closed!"); };

        websocket.OnMessage += bytes =>
        {
            var time = sw.ElapsedMilliseconds;
            timequeue.Enqueue(time);
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

    private async void ActivateTestText()
    {
        var str = @"When predicting the future, such as the longevity of the Berlin Wall, the hypotheses we need to
        evaluate are all the possible durations of the phenomenon at hand: will it last a week, a month, a year,
            a decade? To apply Bayes’s Rule, as we have seen, we first need to assign a prior probability to each
            of these durations. And it turns out that the Copernican Principle is exactly what results from applying
            Bayes’s Rule using what is known as an uninformative prior";
        await ListenTo(str);
    }

    private void PrintQueue()
    {
        foreach (var stamp in timequeue)
        {
            print(stamp);
        }
    }

    private async Task SendWebSocketMessage(string text)
    {
        if (websocket.State == WebSocketState.Open)
            // Sending plain text
            sw.Start(); 
            await websocket.SendText(text);
    }

    public async Task ListenTo(string text)
    {
        print("WebsocketClient listened and sends: " + text);
        await SendWebSocketMessage(text);
    }
}