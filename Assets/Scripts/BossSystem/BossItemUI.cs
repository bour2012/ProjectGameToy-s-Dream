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
        counterText.text = $"{currentCount} / {maxCount}";

        // เช็คสีเขียวทุกครั้งที่มีการอัปเดต UI (เผื่อตอนโหลดซีนใหม่มาแล้วของครบพอดี)
        if (currentCount >= maxCount)
        {
            counterText.color = Color.green;
        }
    }
}