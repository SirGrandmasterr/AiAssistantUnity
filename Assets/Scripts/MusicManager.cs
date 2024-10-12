using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

public class MusicAction : MonoBehaviour
{
    public AudioSource src;
    // Start is called before the first frame update
    private Queue<string> audioqueue;
    public bool isPlaying;


    private void Start()
    {
        audioqueue = new Queue<string>();
        
    }

    private void Update()
    {
        if (Input.GetKeyDown("q"))
        {
        }

        if (Input.GetKeyDown("y"))
        {
            StopPlay();
        }
    }

    private IEnumerator playFromPlaylist(string id)
    {
        using (var webRequest = UnityWebRequestMultimedia.GetAudioClip("https://api.blankframe.com/files/track/download/mp3/" + id, AudioType.MPEG))
        {
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
            src.clip = clip;
            src.Play();
            while (src.isPlaying && isPlaying) yield return null;
            src.Stop();
            Debug.Log("Finished Coroutine");            
        }
    }

    public struct musicFeedback
    {
        public string token;
        public bool found;
        public string query;

    }

    async public Task LoadAudioqueue(string searchterm, string token)
    {
        print("Started LoadAudioqueue");
        searchterm = searchterm.Replace("music", "");
        searchterm = searchterm.Replace("Music", "");
        var client = new HttpClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.blankframe.com/playlist/create/nosave?input=" + searchterm);
        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        print("Awaited request");
        var respstring = await response.Content.ReadAsStringAsync();
        
        BlankframePlaylist playlist = JsonUtility.FromJson<BlankframePlaylist>(respstring);
        var feedback = new musicFeedback();
        feedback.token = token;
        feedback.query = searchterm;
        if (playlist.tracks.Length == 0)
        {
            feedback.found = false;
            print("Broadcasting Feedback: " + feedback.query + " " + "false" + " " + feedback.token);
            gameObject.BroadcastMessage("MusicFound", feedback);
        }
        else
        {
            feedback.found = true;
            gameObject.BroadcastMessage("MusicFound", feedback);
        }
        foreach (var id in playlist.tracks)
        {
            print("Enqueuing track: " + id);
            audioqueue.Enqueue(id);
        }
        
        StartCoroutine(musicrunner());
    }

    public void StopPlay()
    {
        
        audioqueue.Clear();
        isPlaying = false;
    }
    
    
    private IEnumerator musicrunner()
    {
        isPlaying = true;
        while (audioqueue.Count > 0)
        {
            print("Starting Coroutine");
            string id = audioqueue.Dequeue();
            var streamAndPLay = playFromPlaylist(id);
            yield return StartCoroutine(streamAndPLay);
        }

        print("Stopped Playing Music");
        isPlaying = false;
    }
}

[Serializable]
public class BlankframePlaylist
{
    public string title;
    public string type;
    public string[] tracks; 

    
    public static BlankframePlaylist CreateFromJson(string jsonString)
    {
        return JsonUtility.FromJson<BlankframePlaylist>(jsonString);
    }

}


