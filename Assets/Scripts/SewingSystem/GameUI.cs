using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GameUI : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI accuracyText;
    public TextMeshProUGUI scoreText;
    public Button resetButton;
    public Button startButton;
    public Slider accuracySlider;

    [Header("Game References")]
    public SewingController sewingController;

    private float currentScore = 0f;

    private void Start()
    {
        SetupUI();
        SubscribeToEvents();
    }

    void SetupUI()
    {
        if (resetButton != null)
            resetButton.onClick.AddListener(ResetGame);

        if (startButton != null)
            startButton.onClick.AddListener(StartGame);

        UpdateAccuracyDisplay(0f);
        UpdateScoreDisplay(0f);
    }

    void SubscribeToEvents()
    {
        if (sewingController != null)
        {
            sewingController.OnAccuracyUpdated += UpdateAccuracyDisplay;
            sewingController.OnSewingCompleted += OnSewingCompleted;
        }
    }

    void UpdateAccuracyDisplay(float accuracy)
    {
        if (accuracyText != null)
            accuracyText.text = "Accuracy: " + accuracy.ToString("F1") + "%";

        if (accuracySlider != null)
            accuracySlider.value = accuracy / 100f;
    }

    void UpdateScoreDisplay(float score)
    {
        if (scoreText != null)
            scoreText.text = "Score: " + score.ToString("F0");
    }

    void OnSewingCompleted()
    {
        // ¤Ó¹Ç³¤Ðá¹¹¨Ò¡¤ÇÒÁáÁè¹ÂÓ
        float accuracy = sewingController.CalculateCurrentAccuracy();
        currentScore += accuracy * 10; // ¤Ù³ 10 à¾×èÍãËé¤Ðá¹¹´Ù´Õ

        UpdateScoreDisplay(currentScore);

        // áÊ´§¼ÅÅÑ¾¸ì
        ShowResult(accuracy);
    }

    void ShowResult(float accuracy)
    {
        string message = "";
        if (accuracy >= 90f)
            message = "Perfect! ?";
        else if (accuracy >= 75f)
            message = "Great! ??";
        else if (accuracy >= 50f)
            message = "Good try! ??";
        else
            message = "Keep practicing! ??";

        Debug.Log(message + " Accuracy: " + accuracy.ToString("F1") + "%");
    }

    void ResetGame()
    {
        sewingController.ResetSewing();
        currentScore = 0f;
        UpdateScoreDisplay(currentScore);
        UpdateAccuracyDisplay(0f);
    }

    void StartGame()
    {
        ResetGame();
    }

    private void OnDestroy()
    {
        // Unsubscribe events
        if (sewingController != null)
        {
            sewingController.OnAccuracyUpdated -= UpdateAccuracyDisplay;
            sewingController.OnSewingCompleted -= OnSewingCompleted;
        }
    }
}