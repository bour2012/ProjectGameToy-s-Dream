using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TutorialManager : MonoBehaviour
{
    [Header("Tutorial UI")]
    public GameObject tutorialPanel;
    public TextMeshProUGUI instructionText;
    public Image toolHintImage;

    [Header("Tutorial Sprites")]
    public Sprite glueSprite;
    public Sprite threadSprite;
    public Sprite mouseScrollSprite;

    private DollRepairSystem repairSystem;
    private bool tutorialComplete = false;

    void Start()
    {
        repairSystem = Object.FindFirstObjectByType<DollRepairSystem>();
        StartTutorial();
    }

    public void StartTutorial()
    {
        ShowInstruction("เดินเข้าไปใกล้ตุ๊กตาที่พังเพื่อเริ่มซ่อมแซม", null);
    }

    public void OnPlayerNearDoll()
    {
        ShowInstruction("ใช้ลูกกลิ้งเมาส์เพื่อเลือกเครื่องมือ กาว หรือ ด้าย", mouseScrollSprite);
    }

    public void OnRepairSystemOpened(DollRepairSystem.RepairTool tool)
    {
        string toolName = tool == DollRepairSystem.RepairTool.Glue ? "กาว" : "ด้าย";
        ShowInstruction($"ใช้ระบบ{toolName}เพื่อซ่อมแซมตุ๊กตา กด ESC เพื่อออก", null);
    }

    public void OnRepairComplete()
    {
        ShowInstruction("เยี่ยม! คุณซ่อมแซมตุ๊กตาเสร็จแล้ว", null);
        tutorialComplete = true;
        StartCoroutine(HideTutorialAfterDelay(3f));
    }

    void ShowInstruction(string text, Sprite icon)
    {
        tutorialPanel.SetActive(true);
        instructionText.text = text;

        if (icon != null && toolHintImage != null)
        {
            toolHintImage.gameObject.SetActive(true);
            toolHintImage.sprite = icon;
        }
        else if (toolHintImage != null)
        {
            toolHintImage.gameObject.SetActive(false);
        }
    }

    System.Collections.IEnumerator HideTutorialAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        tutorialPanel.SetActive(false);
    }
}
