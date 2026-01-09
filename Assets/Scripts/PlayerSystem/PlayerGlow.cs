using UnityEngine;
using UnityEngine.Rendering.Universal; // ใช้ถ้าเป็น 2D Light

public class PlayerGlow : MonoBehaviour
{
    /*public Light bodyLight; */// ลาก Component Light มาใส่
    public Light2D bodyLight;     // public Light2D bodyLight; // ถ้าใช้ 2D Light

    public float maxIntensity = 1.5f; // ความสว่างสูงสุด
    public float fadeSpeed = 5f;      // ความเร็วในการเปลี่ยนแสง

    private float targetIntensity = 0f;

    void Start()
    {
        if (bodyLight != null) bodyLight.intensity = 0f;
    }

    void Update()
    {
        if (bodyLight != null)
        {
            // คำสั่งนี้จะทำให้ค่าแสงค่อยๆ เปลี่ยนไปหาค่าเป้าหมาย
            bodyLight.intensity = Mathf.Lerp(bodyLight.intensity, targetIntensity, Time.deltaTime * fadeSpeed);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("DarkZone"))
        {
            targetIntensity = maxIntensity; // ตั้งเป้าให้สว่าง
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("DarkZone"))
        {
            targetIntensity = 0f; // ตั้งเป้าให้มืด
        }
    }
}