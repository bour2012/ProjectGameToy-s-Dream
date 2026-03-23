using UnityEngine;
using UnityEngine.UI;

[System.Serializable]
public struct ButtonSoundPair
{
    public Button myButton;
    public AudioClip customSound;
}

public class SceneButtonSound : MonoBehaviour
{
    [Header("���������§��ѡ��Шөҡ")]
    public AudioSource localAudioSource;

    [Header("1. ��駤�һ��� UI ��˹�Ҩ�")]
    public ButtonSoundPair[] uiButtonSounds;

    [Header("2. ��駤�һ�������º (Pressure Plate) 㹩ҡ")]
    public AudioClip pressurePlateSound; // �ҡ���§����º���������ç���������!
    [Header("3. ��駤�Ҥѹ�¡ (Lever)")]
    public AudioClip leverToggleSound;
    void Start()
    {
        if (localAudioSource == null) return;

        // ============================================
        // ��ǹ��� 1: �Ѵ��û��� UI (Ẻ���)
        // ============================================
        foreach (ButtonSoundPair pair in uiButtonSounds)
        {
            if (pair.myButton != null && pair.customSound != null)
            {
                AudioClip clipToPlay = pair.customSound;
                pair.myButton.onClick.AddListener(() =>
                {
                    localAudioSource.PlayOneShot(clipToPlay);
                });
            }
        }

        // ============================================
        // ��ǹ��� 2: �Ѵ��� Pressure Plate �ѵ��ѵ� (����)
        // ============================================
        if (pressurePlateSound != null)
        {
            // �᡹�� PressurePlate �ء��Ƿ���ҧ����㹩ҡ���
            PressurePlate[] allPlates = FindObjectsByType<PressurePlate>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            foreach (PressurePlate plate in allPlates)
            {
                // �ѧ�Ѻ�Ѵ AudioSource ��� ������§ �ҡ�ٹ���ҧ��������ѹ���
                plate.audioSource = localAudioSource;
                plate.activateSound = pressurePlateSound;
            }
            Debug.Log($"�����������§���������º������ {allPlates.Length} �ѹ���º����!");
        }

        if (leverToggleSound != null)
        {
            // �᡹�� Lever �ء���㹩ҡ
            Lever[] allLevers = FindObjectsByType<Lever>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            foreach (Lever lever in allLevers)
            {
                // �� AudioSource ��� ���§�Ѻ�ѹ�¡ ���� Lever �ء���
                lever.audioSource = localAudioSource;
                lever.toggleSound = leverToggleSound;
            }
            Debug.Log($"�����������§���ѹ�¡������ {allLevers.Length} �ѹ���º����!");
        }
    }
}