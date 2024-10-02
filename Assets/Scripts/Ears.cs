using System.Diagnostics;
using UnityEngine;
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
        public bool streamSegments = true;
        public bool printLanguage = true;

        public Text outputText;
        public Text timeText;

        public Brain brain;


        private string _buffer;

        private void Awake()
        {
            whisper.OnNewSegment += OnNewSegment;
            whisper.OnProgress += OnProgressHandler;

            microphoneRecord.OnRecordStop += OnRecordStop;
        }

        private void Update()
        {
            //TODO: Update brain Player audible
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

        public void HearCrashingSound()
        {
            brain.A
        }

        private void OnVadChanged(bool vadStop)
        {
            microphoneRecord.vadStop = vadStop;
        }

        private void OnButtonPressed()
        {
            if (!microphoneRecord.IsRecording)
                microphoneRecord.StartRecord();
            else
                microphoneRecord.StopRecord();
        }

        private async void OnRecordStop(AudioChunk recordedAudio)
        {
            _buffer = "";

            var sw = new Stopwatch();
            sw.Start();

            var res = await whisper.GetTextAsync(recordedAudio.Data, recordedAudio.Frequency, recordedAudio.Channels);
            //brain.SendHistoryUpdate("VISITOR: " + res.Result + "\n", true, "speech");
            brain.SendPlayerSpeech(res.Result);
            //if (res == null || !outputText) 
            //    return;

            /*var time = sw.ElapsedMilliseconds;
            var rate = recordedAudio.Length / (time * 0.001f);
            timeText.text = $"Time: {time} ms\nRate: {rate:F1}x";

            var text = res.Result;
            if (printLanguage)
                text += $"\n\nLanguage: {res.Language}";

            outputText.text = text;*/
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