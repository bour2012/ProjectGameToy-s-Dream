using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ItemDisplayUI : MonoBehaviour
{
    [Header("UI Components")]
    public Image glueIcon;
    public TextMeshProUGUI glueCountText;
    public Image threadIcon;
    public TextMeshProUGUI threadCountText;

    [Header("Visual Settings")]
    public Color normalColor = Color.white;
    public Color emptyColor = Color.red;
    public Color collectColor = Color.green;

    void Start()
    {
        // Subscribe to events
        ItemManager.OnItemCountChanged += OnItemCountChanged;
        ItemManager.OnItemCollected += OnItemCollected;

        // Initial update
        UpdateDisplay();
    }

    void OnDestroy()
    {
        // Unsubscribe from events
        ItemManager.OnItemCountChanged -= OnItemCountChanged;
        ItemManager.OnItemCollected -= OnItemCollected;
    }

    void OnItemCountChanged(ItemManager.ItemType itemType, int newCount)
    {
        UpdateItemDisplay(itemType, newCount);
    }

    void OnItemCollected(ItemManager.ItemType itemType)
    {
        // แสดงเอฟเฟกต์การเก็บไอเท
        StartCoroutine(CollectAnimation(itemType));
    }

    void UpdateDisplay()
    {
        if (ItemManager.Instance == null) return;

        UpdateItemDisplay(ItemManager.ItemType.Glue, ItemManager.Instance.GetItemCount(ItemManager.ItemType.Glue));
        UpdateItemDisplay(ItemManager.ItemType.Thread, ItemManager.Instance.GetItemCount(ItemManager.ItemType.Thread));
    }

    void UpdateItemDisplay(ItemManager.ItemType itemType, int count)
    {
        Color displayColor = count > 0 ? normalColor : emptyColor;

        switch (itemType)
        {
            case ItemManager.ItemType.Glue:
                if (glueCountText != null)
                    glueCountText.text = count.ToString();
                if (glueIcon != null)
                    glueIcon.color = displayColor;
                break;

            case ItemManager.ItemType.Thread:
                if (threadCountText != null)
                    threadCountText.text = count.ToString();
                if (threadIcon != null)
                    threadIcon.color = displayColor;
                break;
        }
    }

    System.Collections.IEnumerator CollectAnimation(ItemManager.ItemType itemType)
    {
        Transform targetIcon = null;

        switch (itemType)
        {
            case ItemManager.ItemType.Glue:
                targetIcon = glueIcon?.transform;
                break;
            case ItemManager.ItemType.Thread:
                targetIcon = threadIcon?.transform;
                break;
        }

        if (targetIcon == null) yield break;

        // แสดงเอฟเฟกต์การเก็บ
        Vector3 originalScale = targetIcon.localScale;

        // ขยายใหญ่
        float time = 0;
        while (time < 0.2f)
        {
            time += Time.deltaTime;
            targetIcon.localScale = Vector3.Lerp(originalScale, originalScale * 1.3f, time / 0.2f);
            yield return null;
        }

        // กลับเป็นปกติ
        time = 0;
        while (time < 0.2f)
        {
            time += Time.deltaTime;
            targetIcon.localScale = Vector3.Lerp(originalScale * 1.3f, originalScale, time / 0.2f);
            yield return null;
        }

        targetIcon.localScale = originalScale;
    }
}
