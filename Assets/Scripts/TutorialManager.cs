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
        ShowInstruction("�Թ��������꡵ҷ��ѧ���������������", null);
    }

    public void OnPlayerNearDoll()
    {
        ShowInstruction("���١����������������͡����ͧ��� ��� ���� ����", mouseScrollSprite);
    }

    public void OnRepairSystemOpened(DollRepairSystem.RepairTool tool)
    {
        string toolName = tool == DollRepairSystem.RepairTool.Glue ? "���" : "����";
        ShowInstruction($"���к�{toolName}���ͫ�������꡵� �� ESC �����͡", null);
    }

    public void OnRepairComplete()
    {
        ShowInstruction("������! �س��������꡵���������", null);
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
