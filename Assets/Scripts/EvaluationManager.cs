using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement; // Required for controller navigation focus

public class EvaluationManager : MonoBehaviour
{
    [Tooltip("Assign the four evaluation buttons here in order (Button 1, 2, 3, 4).")]
    public Button[] evaluationButtons;
    public Button resetButton;

    [Tooltip("The color the correct button will change to when clicked.")]
    public Color correctButtonColor = new Color(0.6f, 1f, 0.6f, 1f); // A pleasant light green
    [Tooltip("The color every irrelevant non-clicked button will change to.")]
    public Color notClickedButtonColor = new Color(0.5f, 0.5f, 0.5f, 1f);
    [Tooltip("The color the wrong button will change to when clicked.")]
    public Color wrongButtonColor = new Color(1f, 0.35f, 0.4f, 1f);
    
   
    private string correctScenarioKey = "scenarioInt"; // The PlayerPrefs key to check.
    public bool isButtonSelected = false; // Prevents further interaction after a choice is made.


    void Start()
    {   //REMOVE THIS LATER
        PlayerPrefs.SetInt("scenario", 3);
        resetButton.enabled = false;
        resetButton.onClick.AddListener(ResetScenario);
        Debug.Log("Correct Button Color: " + correctButtonColor);
        Debug.Log("Not Clicked Button Color: " + notClickedButtonColor);
        Debug.Log("Wrong Button Color: " + wrongButtonColor);
        correctButtonColor = new Color(0.6f, 1f, 0.6f, 1f); 
        notClickedButtonColor = new Color(0.5f, 0.5f, 0.5f, 1f);
        wrongButtonColor = new Color(1f, 0.35f, 0.4f, 1f);
        
        // Ensure buttons are assigned to prevent errors.
        if (evaluationButtons == null || evaluationButtons.Length != 4)
        {
            Debug.LogError("EvaluationManager: Please assign exactly 4 buttons in the Inspector.");
            return;
        }

        // Add an onClick listener programmatically to each button.
        for (int i = 0; i < evaluationButtons.Length; i++)
        {
            Debug.Log("Adding Listener");
            int buttonIndex = i; // Create a local copy for the closure below.
            evaluationButtons[i].onClick.AddListener(() => OnEvaluationButtonClicked(buttonIndex));
            evaluationButtons[i].interactable = true;
        }
        
        EventSystem.current.SetSelectedGameObject(evaluationButtons[0].gameObject);
    }
    
    public void OnEvaluationButtonClicked(int clickedButtonIndex)
    {
        // If a selection has already been made, do nothing.
        if (isButtonSelected)
        {
            return;
        }
        isButtonSelected = true;

        // Retrieve the correct scenario number from PlayerPrefs.
        // It defaults to 1 if the key is not found.
        // The key should be set in a previous scene, e.g., PlayerPrefs.SetInt("scenario", 3);
        int correctScenarioIndex = PlayerPrefs.GetInt(correctScenarioKey, 1) - 1; // Convert 1-4 to 0-3 index

        for (int i = 0; i < evaluationButtons.Length; i++)
        {
            if (i != correctScenarioIndex && i != clickedButtonIndex)
            {
                Image nonClickedButtonImage = evaluationButtons[i].GetComponent<Image>();
                if (nonClickedButtonImage != null)
                {
                    nonClickedButtonImage.color = notClickedButtonColor;
                }
            }
        }
        // Check if the clicked button was the correct one.
        if (clickedButtonIndex == correctScenarioIndex)
        {
            // Get the Image component of the button to change its color.
            Image buttonImage = evaluationButtons[clickedButtonIndex].GetComponent<Image>();
            if (buttonImage != null)
            {
                buttonImage.color = correctButtonColor;
            }
        }
        else
        {
            Image wrongButtonImage = evaluationButtons[clickedButtonIndex].GetComponent<Image>();
            if (wrongButtonImage != null)
            {
                wrongButtonImage.color = wrongButtonColor;
            }
            
        }
        Image trueButtonImage = evaluationButtons[correctScenarioIndex].GetComponent<Image>();
        if (trueButtonImage != null)
        {
            trueButtonImage.color = correctButtonColor;
        }
        
        // Deactivate all buttons to prevent further clicks.
        DeactivateAllButtons();
        resetButton.enabled = true;
    }

    private void ResetScenario()
    {
        SceneManager.LoadScene("ScenarioSelect");
    }
    
    private void DeactivateAllButtons()
    {
        // To ensure no button remains selected for navigation, clear the Event System's focus.
        EventSystem.current.SetSelectedGameObject(null);
        
        foreach (Button btn in evaluationButtons)
        {
            btn.interactable = false;
        }
    }
}
