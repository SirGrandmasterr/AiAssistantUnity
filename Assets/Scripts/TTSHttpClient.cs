using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;

public class SoundGetter : MonoBehaviour
{
    [SerializeField] public AudioSource source;
    public bool isHandling;

    public Queue<bool> RemainingAudioFiles = new();
    public Brain brain;

    private void Start()
    {
    }

    private void Update()
    {
        if (Input.GetKeyDown("b"))
        {
            var streamAndPLay = SuckAndPlayA();
            StartCoroutine(streamAndPLay);
        }
    }

    private IEnumerator SuckAndPlayA()
    {
        using (var webRequest = UnityWebRequestMultimedia.GetAudioClip("http://localhost:8001/", AudioType.WAV))
        {
            print("SuckAndPlayA");
            print("Remaining Audio files: " + RemainingAudioFiles.Count());
            RemainingAudioFiles.Dequeue();
            print("Remaining Audio files after Dequeue: " + RemainingAudioFiles.Count());

            ((DownloadHandlerAudioClip)webRequest.downloadHandler).streamAudio = true;

            webRequest.SendWebRequest();

            while (!(webRequest.result == UnityWebRequest.Result.ConnectionError) && webRequest.downloadedBytes < 1024)
                
                yield return null;

            if (webRequest.result == UnityWebRequest.Result.ConnectionError)
            {
                Debug.LogError(webRequest.error);
                yield break;
            }

            var clip = ((DownloadHandlerAudioClip)webRequest.downloadHandler).audioClip;
            source.clip = clip;
            print("clip length: " + clip.length);
            source.Play();
            while (source.isPlaying) yield return null;
            Debug.Log("Finished Coroutine");            
        }
    }


    private IEnumerator soundmanager()
    {
        brain.isSpeaking = true;
        isHandling = true;
        while (RemainingAudioFiles.Count() > 0)
        {
            print("Starting Coroutine");
            var streamAndPLay = SuckAndPlayA();
            yield return StartCoroutine(streamAndPLay);
        }

        print("Stopping Handling");
        isHandling = false;
        brain.isSpeaking = false;
        yield return null;
    }

    public void AddToQueue()
    {
        RemainingAudioFiles.Enqueue(true);
        if (!isHandling)
        {
            var soundmanager = this.soundmanager();
            StartCoroutine(soundmanager);
            print("Returning from AddToQueueIf");
        }
        print("Returning from AddToQueue");
    }
}