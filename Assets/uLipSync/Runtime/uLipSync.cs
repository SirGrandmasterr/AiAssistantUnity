using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using System.Collections.Generic;

namespace uLipSync
{

public class uLipSync : MonoBehaviour
{
    public Profile profile;
    public LipSyncUpdateEvent onLipSyncUpdate = new LipSyncUpdateEvent();
    [Range(0f, 1f)] public float outputSoundGain = 1f;

    // --- NEW ---
    // When true, this component will not automatically read from an AudioSource.
    // Audio must be provided externally by calling the InjectAudioData() method.
    public bool manualAudioInput = false;

    AudioSource _audioSource;
    public uLipSyncAudioSource audioSourceProxy;
    uLipSyncAudioSource _currentAudioSourceProxy;

    JobHandle _jobHandle;
    object _lockObject = new object();
    bool _allocated = false;
    int _index = 0;
    bool _isDataReceived = false;

    NativeArray<float> _rawInputData;
    NativeArray<float> _inputData;
    NativeArray<float> _mfcc;
    NativeArray<float> _mfccForOther;
    NativeArray<float> _means;
    NativeArray<float> _standardDeviations;
    NativeArray<float> _phonemes;
    NativeArray<float> _scores;
    NativeArray<LipSyncJob.Info> _info;
    List<int> _requestedCalibrationVowels = new List<int>();
    Dictionary<string, float> _ratios = new Dictionary<string, float>();

    public NativeArray<float> mfcc => _mfccForOther;
    public LipSyncInfo result { get; private set; } = new LipSyncInfo();
    
#if UNITY_WEBGL
    public bool autoAudioSyncOnWebGL = true;
    [Range(-0.1f, 0.3f)] public float audioSyncOffsetTime = 0f;
    #if !UNITY_EDITOR
    float[] _audioBuffer = null;
    #endif
    bool _isWebGLProcessed = false;
#endif
    
#if ULIPSYNC_DEBUG
    NativeArray<float> _debugData;
    NativeArray<float> _debugSpectrum;
    NativeArray<float> _debugMelSpectrum;
    NativeArray<float> _debugMelCepstrum;
    NativeArray<float> _debugDataForOther;
    NativeArray<float> _debugSpectrumForOther;
    NativeArray<float> _debugMelSpectrumForOther;
    NativeArray<float> _debugMelCepstrumForOther;
    public NativeArray<float> data => _debugDataForOther;
    public NativeArray<float> spectrum => _debugSpectrumForOther;
    public NativeArray<float> melSpectrum => _debugMelSpectrumForOther;
    public NativeArray<float> melCepstrum => _debugMelCepstrumForOther;
#endif

    int inputSampleCount
    {
        get 
        {  
            if (!profile) return AudioSettings.outputSampleRate;
            float r = (float)AudioSettings.outputSampleRate / profile.targetSampleRate;
            return Mathf.CeilToInt(profile.sampleCount * r);
        }
    }
    
    int mfccNum => profile ? profile.mfccNum : 12;

    void Awake()
    {
        UpdateAudioSource();
        UpdateAudioSourceProxy();

#if UNITY_WEBGL && !UNITY_EDITOR
        InitializeWebGL();
#endif
    }

    void OnEnable()
    {
        AllocateBuffers();
    }

    void OnDisable()
    {
        _jobHandle.Complete();
        DisposeBuffers();
    }

    void Update()
    {
        if (!profile) return;
        if (!_jobHandle.IsCompleted) return;

#if UNITY_WEBGL && !UNITY_EDITOR
        UpdateWebGL();
#endif
        UpdateResult();
        InvokeCallback();
        UpdateCalibration();
        UpdatePhonemes();
        ScheduleJob();

        UpdateBuffers();
        UpdateAudioSource();
        UpdateAudioSourceProxy();
    }

    void AllocateBuffers()
    {
        if (_allocated)
        {
            DisposeBuffers();
        }
        _allocated = true;

        _jobHandle.Complete();

        lock (_lockObject)
        {
            int n = inputSampleCount;
            int phonemeCount = profile ? profile.mfccs.Count : 1;
            _rawInputData = new NativeArray<float>(n, Allocator.Persistent);
            _inputData = new NativeArray<float>(n, Allocator.Persistent); 
            _mfcc = new NativeArray<float>(mfccNum, Allocator.Persistent); 
            _mfccForOther = new NativeArray<float>(mfccNum, Allocator.Persistent); 
            _means = new NativeArray<float>(mfccNum, Allocator.Persistent); 
            _standardDeviations = new NativeArray<float>(mfccNum, Allocator.Persistent); 
            _scores = new NativeArray<float>(phonemeCount, Allocator.Persistent);
            _phonemes = new NativeArray<float>(mfccNum * phonemeCount, Allocator.Persistent);
            _info = new NativeArray<LipSyncJob.Info>(1, Allocator.Persistent);
#if ULIPSYNC_DEBUG
            _debugData = new NativeArray<float>(profile.sampleCount, Allocator.Persistent);
            _debugDataForOther = new NativeArray<float>(profile.sampleCount, Allocator.Persistent);
            _debugSpectrum = new NativeArray<float>(profile.sampleCount, Allocator.Persistent);
            _debugSpectrumForOther = new NativeArray<float>(profile.sampleCount, Allocator.Persistent);
            _debugMelSpectrum = new NativeArray<float>(profile.melFilterBankChannels, Allocator.Persistent);
            _debugMelSpectrumForOther = new NativeArray<float>(profile.melFilterBankChannels, Allocator.Persistent);
            _debugMelCepstrum = new NativeArray<float>(profile.melFilterBankChannels, Allocator.Persistent);
            _debugMelCepstrumForOther = new NativeArray<float>(profile.melFilterBankChannels, Allocator.Persistent);
#endif
        }
    }

    void DisposeBuffers()
    {
        if (!_allocated) return;
        _allocated = false;

        _jobHandle.Complete();

        lock (_lockObject)
        {
            if (_rawInputData.IsCreated) _rawInputData.Dispose();
            if (_inputData.IsCreated) _inputData.Dispose();
            if (_mfcc.IsCreated) _mfcc.Dispose();
            if (_mfccForOther.IsCreated) _mfccForOther.Dispose();
            if (_means.IsCreated) _means.Dispose();
            if (_standardDeviations.IsCreated) _standardDeviations.Dispose();
            if (_scores.IsCreated) _scores.Dispose();
            if (_phonemes.IsCreated) _phonemes.Dispose();
            if (_info.IsCreated) _info.Dispose();
#if ULIPSYNC_DEBUG
            if (_debugData.IsCreated) _debugData.Dispose();
            if (_debugDataForOther.IsCreated) _debugDataForOther.Dispose();
            if (_debugSpectrum.IsCreated) _debugSpectrum.Dispose();
            if (_debugSpectrumForOther.IsCreated) _debugSpectrumForOther.Dispose();
            if (_debugMelSpectrum.IsCreated) _debugMelSpectrum.Dispose();
            if (_debugMelSpectrumForOther.IsCreated) _debugMelSpectrumForOther.Dispose();
            if (_debugMelCepstrum.IsCreated) _debugMelCepstrum.Dispose();
            if (_debugMelCepstrumForOther.IsCreated) _debugMelCepstrumForOther.Dispose();
#endif
        }
    }

    void UpdateBuffers()
    {
        if (!_allocated) return;
        if (inputSampleCount != _rawInputData.Length ||
            (profile && profile.mfccs.Count * mfccNum != _phonemes.Length)
#if ULIPSYNC_DEBUG
            || (profile && profile.melFilterBankChannels != _debugMelSpectrum.Length)
#endif
        )
        {
            lock (_lockObject)
            {
                DisposeBuffers();
                AllocateBuffers();
            }
        }
    }

    void UpdateResult()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        if (!_isWebGLProcessed)
        {
            result = new LipSyncInfo()
            {
                phoneme = result.phoneme,
                volume = 0f,
                rawVolume = 0f,
                phonemeRatios = _ratios,
            };
            return;
        }
#endif
        
        _jobHandle.Complete();
        _mfccForOther.CopyFrom(_mfcc);
        
#if ULIPSYNC_DEBUG
        _debugDataForOther.CopyFrom(_debugData);
        _debugSpectrumForOther.CopyFrom(_debugSpectrum);
        _debugMelSpectrumForOther.CopyFrom(_debugMelSpectrum);
        _debugMelCepstrumForOther.CopyFrom(_debugMelCepstrum);
#endif
        
        int index = _info[0].mainPhonemeIndex;
        string mainPhoneme = profile.GetPhoneme(index);

        float sumScore = 0f;
        for (int i = 0; i < _scores.Length; ++i)
        {
            sumScore += _scores[i];
        }

        _ratios.Clear();
        for (int i = 0; i < _scores.Length; ++i)
        {
            var phoneme = profile.GetPhoneme(i);
            var ratio = sumScore > 0f ? _scores[i] / sumScore : 0f;
            if (!_ratios.ContainsKey(phoneme))
            {
                _ratios.Add(phoneme, 0f);
            }
            _ratios[phoneme] += ratio;
        }

        float rawVol = _info[0].volume;
        float minVol = Common.DefaultMinVolume;
        float maxVol = Common.DefaultMaxVolume;
        float normVol = Mathf.Log10(rawVol);
        normVol = (normVol - minVol) / (maxVol - minVol);
        normVol = Mathf.Clamp(normVol, 0f, 1f);

        result = new LipSyncInfo()
        {
            phoneme = mainPhoneme,
            volume = normVol,
            rawVolume = rawVol,
            phonemeRatios = _ratios,
        };
    }

    void InvokeCallback()
    {
        if (onLipSyncUpdate == null) return;

        onLipSyncUpdate.Invoke(result);
    }

    void UpdatePhonemes()
    {
        if (!profile) return;
        int index = 0;
        foreach (var data in profile.mfccs)
        {
            foreach (var value in data.mfccNativeArray)
            {
                if (index >= _phonemes.Length) break;
                _phonemes[index++] = value;
            }
        }
    }

    void ScheduleJob()
    {
        if (!_isDataReceived) return;
        _isDataReceived = false;

        int index = 0;
        lock (_lockObject)
        {
            _inputData.CopyFrom(_rawInputData);
            if(profile) {
                _means.CopyFrom(profile.means);
                _standardDeviations.CopyFrom(profile.standardDeviation);
            }
            index = _index;
        }

        var lipSyncJob = new LipSyncJob()
        {
            input = _inputData,
            startIndex = index,
            outputSampleRate = AudioSettings.outputSampleRate,
            targetSampleRate = profile.targetSampleRate,
            melFilterBankChannels = profile.melFilterBankChannels,
            means = _means,
            standardDeviations = _standardDeviations,
            mfcc = _mfcc,
            phonemes = _phonemes,
            compareMethod = profile.compareMethod,
            scores = _scores,
            info = _info,
#if ULIPSYNC_DEBUG
            debugData = _debugData,
            debugSpectrum = _debugSpectrum,
            debugMelSpectrum = _debugMelSpectrum,
            debugMelCepstrum = _debugMelCepstrum,
#endif
        };

        _jobHandle = lipSyncJob.Schedule();
    }

    public void RequestCalibration(int index)
    {
        _requestedCalibrationVowels.Add(index);
    }

    void UpdateCalibration()
    {
        if (!profile) return;

        foreach (var index in _requestedCalibrationVowels)
        {
            profile.UpdateMfcc(index, mfcc, true);
        }

        _requestedCalibrationVowels.Clear();
    }

    void UpdateAudioSource()
    {
        if (_audioSource) return;

        _audioSource = GetComponent<AudioSource>();
    }

    void UpdateAudioSourceProxy()
    {
        // --- MODIFIED ---
        // If in manual mode, do not manage proxy.
        if (manualAudioInput)
        {
            if (_currentAudioSourceProxy)
            {
                _currentAudioSourceProxy.onAudioFilterRead.RemoveListener(OnDataReceived);
                _currentAudioSourceProxy = null;
            }
            return;
        }

        if (audioSourceProxy == _currentAudioSourceProxy) return;

        if (_currentAudioSourceProxy)
        {
            _currentAudioSourceProxy.onAudioFilterRead.RemoveListener(OnDataReceived);
        }

        if (audioSourceProxy)
        {
            audioSourceProxy.onAudioFilterRead.AddListener(OnDataReceived);
        }

        _currentAudioSourceProxy = audioSourceProxy;
    }
    
    private void ProcessAudio(float[] input, int channels)
    {
        if (!_allocated || !_rawInputData.IsCreated || _rawInputData.Length == 0) return;
        Debug.Log("DID NOT RETURN");
        lock (_lockObject)
        {
            int n = _rawInputData.Length;
            for (int i = 0; i < input.Length; i += channels) 
            {
                _rawInputData[_index] = input[i];
                _index = (_index + 1) % n;
            }
        }

        _isDataReceived = true;
    }

    /// <summary>
    /// --- NEW METHOD FOR REAL-TIME STREAMING ---
    /// Public method to inject audio data from any source (like WebRTC).
    /// This allows for real-time analysis without a traditional AudioSource.
    /// Assumes mono audio data.
    /// </summary>
    /// <param name="data">A float array of raw audio data.</param>
    public void InjectAudioData(float[] data)
    {
        // Process the injected data as a mono stream.
        ProcessAudio(data, 1);
    }

    /// <summary>
    /// This is called by the uLipSyncAudioSource proxy.
    /// It now calls the centralized ProcessAudio method.
    /// </summary>
    public void OnDataReceived(float[] input, int channels)
    {
        ProcessAudio(input, channels);

        if (math.abs(outputSoundGain - 1f) > math.EPSILON)
        {
            int n = input.Length;
            for (int i = 0; i < n; ++i) 
            {
                input[i] *= outputSoundGain;
            }
        }
    }
    
    void OnAudioFilterRead(float[] input, int channels)
    {
        // --- MODIFIED ---
        // If in manual mode, do nothing here. Audio is injected externally.
        if (manualAudioInput) return;
        if (audioSourceProxy) return;
        
        OnDataReceived(input, channels);
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    public void InitializeWebGL()
    {
        if (!_audioSource) return;

        if (autoAudioSyncOnWebGL)
        {
            WebGL.Register(this);
        }
    }

    public void OnAuidoContextInitiallyResumed()
    {
        if (!_audioSource) return;

        _audioSource.timeSamples = _audioSource.timeSamples;

        Debug.Log("AudioSource.timeSamples has been automatically synchronized.");
    }

    void UpdateWebGL()
    {
        _isWebGLProcessed = false;

        if (!_audioSource || !_audioSource.isPlaying) return;

        var clip = _audioSource.clip;
        if (!clip || clip.loadState != AudioDataLoadState.Loaded) return;

        int ch = clip.channels;
        int fps = Application.targetFrameRate;
        if (fps <= 0) fps = 60;
        int n = AudioSettings.outputSampleRate * ch / fps;

        if (_audioBuffer == null || _audioBuffer.Length != n)
        {
            _audioBuffer = new float[n];
        }

        int offset = _audioSource.timeSamples;
        offset += (int)(audioSyncOffsetTime * AudioSettings.outputSampleRate * ch);
        offset = math.min(offset, clip.samples - n - 2);
        clip.GetData(_audioBuffer, offset);
        OnDataReceived(_audioBuffer, ch);

        _isWebGLProcessed = true;
    }
#endif

#if UNITY_EDITOR
    public void OnBakeStart(Profile profile)
    {
        this.profile = profile;
        AllocateBuffers();
    }

    public void OnBakeEnd()
    {
        _jobHandle.Complete();
        DisposeBuffers();
    }

    public void OnBakeUpdate(float[] input, int channels)
    {
        OnDataReceived(input, channels);
        UpdateBuffers();
        UpdatePhonemes();
        ScheduleJob();
        _jobHandle.Complete();
        UpdateResult();
    }
#endif
}

}
