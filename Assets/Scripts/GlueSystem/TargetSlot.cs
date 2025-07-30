using UnityEngine;

public class TargetSlot : MonoBehaviour
{
    [Header("Slot Settings")]
    public int targetPieceID;
    public Color slotColor = Color.gray;

    private Renderer slotRenderer;

    void Start()
    {
        slotRenderer = GetComponent<Renderer>();
        if (slotRenderer != null)
        {
            slotRenderer.material.color = slotColor;
        }
    }

    void OnDrawGizmos()
    {
        // แสดงตำแหน่งเป้าหมายใน Scene View
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(transform.position, Vector3.one * 0.5f);
    }
}
