using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

/// <summary>
/// Manages the time a player spends in a scene.
/// If the time exceeds a set limit, it transitions to a specified end scene.
/// Provides a grace period if a specific action is taken near the time limit.
/// </summary>
public class SceneTimeManager : MonoBehaviour
{
    // --- PUBLIC FIELDS ---
    [Header("Time Settings")]
    [Tooltip("The name of the scene to load when the time is up.")]
    public string endSceneName = "EndScreen";

    [Tooltip("The maximum time in seconds the player can stay in the scene.")]
    public float maxTimeInScene = 300f; // 5 minutes

    [Tooltip("The grace period in seconds to add if the last recording is made near the end.")]
    public float gracePeriod = 30f; // 30 seconds

    [Header("Recording Action Settings")]
    [Tooltip("The maximum number of recording actions allowed.")]
    public int maxRecordings = 3;

    [Tooltip("How close to the end (in seconds) a recording must be to grant a grace period.")]
    public float gracePeriodThreshold = 10f; // If last recording is within 30s of the 5-min mark

    // --- PRIVATE FIELDS ---
    private float currentTime = 0f;
    private int recordingCount = 0;
    private float lastRecordingTime = -1f;
    private bool gracePeriodGranted = false;
    private bool isTransitioning = false;


    // --- UNITY METHODS ---

    void Update()
    {
        // Don't do anything if we are already changing scenes.
        if (isTransitioning)
        {
            return;
        }

        // Increment the timer.
        currentTime += Time.deltaTime;

        // Check if the time limit has been exceeded.
        CheckTimeLimit();
    }


    // --- PUBLIC METHODS ---

    /// <summary>
    /// This method should be called by the other script whenever a speech recording action is performed.
    /// </summary>
    public void RecordAction()
    {
        if (recordingCount < maxRecordings)
        {
            recordingCount++;
            lastRecordingTime = currentTime;
            Debug.Log($"Recording action #{recordingCount} taken at {currentTime} seconds.");
        }
        else
        {
            if (!isTransitioning)
            {
                Debug.Log("Time limit reached. Starting scene transition.");
                isTransitioning = true;
                // You can either load the scene directly or start a fade coroutine.
                // SceneManager.LoadScene(endSceneName); 
                StartCoroutine(FadeToScene(endSceneName, 5.0f));
            }
        }
    }


    // --- PRIVATE LOGIC ---

    private void CheckTimeLimit()
    {
        // Check if we've reached the time limit
        if (currentTime >= maxTimeInScene)
        {
            // Check if we should grant a grace period
            // This happens only once
            if (!gracePeriodGranted && recordingCount == maxRecordings)
            {
                // Check if the last recording was made within the threshold period before the initial max time
                bool isLastRecordingRecent = (maxTimeInScene - lastRecordingTime) <= gracePeriodThreshold;

                if (isLastRecordingRecent)
                {
                    // Grant the grace period by extending the max time
                    maxTimeInScene += gracePeriod;
                    gracePeriodGranted = true; // Ensure this logic only runs once
                    Debug.Log($"Grace period granted. New max time is {maxTimeInScene} seconds.");
                    return; // Exit the function for this frame to let the new time take effect
                }
            }
            
            // If the time is up (either initial or extended), start the scene transition.
            // We set 'isTransitioning' to true to prevent this from being called multiple times.
            if (!isTransitioning)
            {
                 Debug.Log("Time limit reached. Starting scene transition.");
                 isTransitioning = true;
                 // You can either load the scene directly or start a fade coroutine.
                 // SceneManager.LoadScene(endSceneName); 
                 StartCoroutine(FadeToScene(endSceneName, 5.0f));
            }
        }
    }

    /// <summary>
    /// Coroutine to fade the screen to black and then load the next scene.
    /// </summary>
    /// <param name="sceneName">The name of the scene to load.</param>
    /// <param name="fadeDuration">The duration of the fade in seconds.</param>
    private IEnumerator FadeToScene(string sceneName, float fadeDuration)
    {
        // Create a new GameObject for the fade panel
        GameObject fadePanelObject = new GameObject("FadePanel");
        Canvas canvas = fadePanelObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000; // Ensure it's on top of everything

        UnityEngine.UI.Image fadeImage = fadePanelObject.AddComponent<UnityEngine.UI.Image>();
        fadeImage.color = new Color(0, 0, 0, 0); // Start fully transparent

        float timer = 0f;
        while (timer < fadeDuration)
        {
            // Calculate the new alpha value
            float alpha = Mathf.Lerp(0, 1, timer / fadeDuration);
            fadeImage.color = new Color(0, 0, 0, alpha);
            
            timer += Time.deltaTime;
            yield return null; // Wait for the next frame
        }

        // Ensure it's fully black before loading the scene
        fadeImage.color = new Color(0, 0, 0, 1);

        // Load the new scene
        SceneManager.LoadScene(sceneName);
    }
}
