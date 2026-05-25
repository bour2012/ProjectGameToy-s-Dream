using UnityEngine;
using TMPro; // อย่าลืม using ตัวนี้

public class BossItemUI : MonoBehaviour
{
    public TextMeshProUGUI counterText; // ลาก Text ใน Hierarchy มาใส่
    private int currentCount = 0;
    public int maxCount = 3; // เปลี่ยนเป็น public เพื่อให้ปรับเป้าหมายใน Inspector ได้ง่ายๆ

    void Start()
    {
        // โหลดค่าเดิมที่เคยเก็บไว้ (ถ้าไม่เคยมีค่านี้มาก่อน ให้เริ่มที่ 0)
        currentCount = PlayerPrefs.GetInt("BossItemCount", 0);
        UpdateUI();
    }

    // ฟังก์ชันสำหรับเพิ่มแต้ม
    public void AddBossItem()
    {
        currentCount++;

        // บันทึกค่าลงระบบทันทีที่เก็บได้ (ตายหรือโหลดซีนใหม่ก็ยังอยู่)
        PlayerPrefs.SetInt("BossItemCount", currentCount);
        PlayerPrefs.Save();

        UpdateUI();
    }

    void UpdateUI()
    {
       if (counterText == null) 
        {
            Debug.LogWarning("BossItemUI: ลืมลาก Text ใส่ช่อง Counter Text ใน Inspector ครับ!");
            return; // สั่งหยุดการทำงานของฟังก์ชันนี้ทันที เพื่อไม่ให้ลงไปเจอ Error ด้านล่าง
        }

        // โค้ดเดิมของคุณ
        counterText.text = $"{currentCount} / {maxCount}";

        if (currentCount >= maxCount)
        {
            counterText.color = Color.green;
        }
    }
}