using System.Collections;
using System.Diagnostics;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using Whisper.Utils;
using Debug = UnityEngine.Debug;

namespace Whisper.Ears
{
    /// <summary>
    ///     Record audio clip from microphone and make a transcription.
    /// </summary>
    public class Ears : MonoBehaviour
    {
        public WhisperManager whisper;
        public MicrophoneRecord microphoneRecord;
        public AudioClip testClip;
        public bool streamSegments = true;
        public bool printLanguage = true;
        [FormerlySerializedAs("Player")] public GameObject player;

        public Text outputText;
        public Text timeText;
        private Vector3 _heightOffset;
        public SceneTimeManager timeManager;
        private Stopwatch recordingStopwatch;

        public Brain brain;

        
        private string _buffer;

        private void Awake()
        {
            _heightOffset = new Vector3(0f, 1.7f, 0);
            whisper.OnNewSegment += OnNewSegment;
            whisper.OnProgress += OnProgressHandler;
            recordingStopwatch = new Stopwatch();

            microphoneRecord.OnRecordStop += OnRecordStop;
            StartCoroutine(UpdatePlayerAudible());
        }

        private void Update()
        {
            if (Input.GetKeyDown("e"))
            {
                print("Pressed e");
                OnButtonPressed();
            }
            
           

            if (Input.GetKeyUp("e"))
            {
                print("Released e");
                OnButtonPressed();
            }
        }
        
        private IEnumerator UpdatePlayerAudible()
        {
            while (true)
            {
                //Check Visibility not every frame, but every 0.2 seconds. Same for Conversation state. 
                yield return new WaitForSeconds(0.2f);
                if (20f < CheckAssistantPlayerDistance())
                {
                    brain.PlayerAudible = false;
                }
                else
                {
                    brain.PlayerAudible = true;
                }
                
            }
        }
        
        private float CheckAssistantPlayerDistance()
        {
            return Vector3.Distance(transform.position + _heightOffset, player.transform.position + _heightOffset);
        }

        public void HearCrashingSound(GameObject obj, string eventLocation)
        {
            string[] opts;
            if (brain.AssetsInView.Contains(obj.name))
            {
                Brain.EventContext ctxInView;
                ctxInView = new Brain.EventContext();
                ctxInView.eventLocation = eventLocation;
                ctxInView.relevantObjects = new[] { obj.name };
                opts = new[] { "ignore", "repair" };
                brain.AddToRepairQueue(obj);
                brain.SendEnvEvent("NARRATOR: Visitor bumped into a sculpture, breaking the glass around it.", ctxInView, opts);
                return;
            } 
            Brain.EventContext ctxOutOfView;
            ctxOutOfView = new Brain.EventContext();
            opts = new[] { "ignore", "investigate" };
            ctxOutOfView.eventLocation = eventLocation;
            brain.AddToRepairQueue(obj);
            brain.SendEnvEvent("NARRATOR: The Assistant hears the sound of glass breaking, somewhere in the " + eventLocation, ctxOutOfView, opts);
        }

        private void OnVadChanged(bool vadStop)
        {
            microphoneRecord.vadStop = vadStop;
        }

        private void OnButtonPressed()
        {
            if (!microphoneRecord.IsRecording)
            {
                microphoneRecord.StartRecord();
                recordingStopwatch.Start();
            }
            else
            {
                microphoneRecord.StopRecord();
                Debug.Log(recordingStopwatch.ElapsedMilliseconds);
                if (recordingStopwatch.ElapsedMilliseconds < 500)
                {
                    recordingStopwatch.Stop();
                    recordingStopwatch.Reset();
                }
                else
                {
                    Debug.Log("Recording Action");
                    timeManager.RecordAction();
                    recordingStopwatch.Stop();
                    recordingStopwatch.Reset();
                }
            }
        }
        
        public async void ButtonPressedTest()
        {
            _buffer = "";

            var sw = new Stopwatch();
            sw.Start();
            
            var res = await whisper.GetTextAsync(testClip);
            if (res == null) 
                return;

            var time = sw.ElapsedMilliseconds;
            var rate = testClip.length / (time * 0.001f);
            print( $"Time: {time} ms\nRate: {rate:F1}x");

            var text = res.Result;
            if (printLanguage)
                text += $"\n\nLanguage: {res.Language}";
        }

        private async void OnRecordStop(AudioChunk recordedAudio)
        {
            _buffer = "";

            var sw = new Stopwatch();
            sw.Start();

            var res = await whisper.GetTextAsync(recordedAudio.Data, recordedAudio.Frequency, recordedAudio.Channels);
            //brain.SendHistoryUpdate("VISITOR: " + res.Result + "\n", true, "speech");
            if (brain.PlayerAudible)
            {
                brain.SendPlayerSpeech(res.Result);
            }
            //if (res == null || !outputText) 
            //    return;

            var time = sw.ElapsedMilliseconds;
            var rate = recordedAudio.Length / (time * 0.001f);
            //timeText.text = $"Time: {time} ms\nRate: {rate:F1}x";

            var text = res.Result;
            if (printLanguage)
                text += $"\n\nLanguage: {res.Language}";

            //outputText.text = text;
        }
        
        private void OnProgressHandler(int progress)
        {
            if (!timeText)
                return;
            timeText.text = $"Progress: {progress}%";
        }

        private void OnNewSegment(WhisperSegment segment)
        {
            if (!streamSegments || !outputText)
                return;

            _buffer += segment.Text;
            outputText.text = _buffer + "...";
        }
    }
}