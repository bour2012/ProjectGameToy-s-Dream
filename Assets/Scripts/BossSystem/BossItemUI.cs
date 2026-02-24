using UnityEngine;
using TMPro; // อย่าลืม using ตัวนี้

public class BossItemUI : MonoBehaviour
{
    public TextMeshProUGUI counterText; // ลาก Text ใน Hierarchy มาใส่
    private int currentCount = 0;
    private int maxCount = 3; // ตั้งค่าเป้าหมาย เช่น 3 ชิ้น

    void Start()
    {
        UpdateUI();
    }

    // ฟังก์ชันสำหรับเพิ่มแต้ม
    public void AddBossItem()
    {
        currentCount++;
        UpdateUI();

        if (currentCount >= maxCount)
        {
            counterText.color = Color.green; 

        }
    }

    void UpdateUI()
    {
        counterText.text = $"{currentCount} / {maxCount}";
    }
}