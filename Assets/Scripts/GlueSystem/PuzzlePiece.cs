using UnityEngine;

public class PuzzlePiece : MonoBehaviour
{
    [Header("Piece Settings")]
    public int pieceID;
    public Vector2 correctPosition;
    public float correctRotation;
    public float snapDistance = 1.0f; // เพิ่มระยะทำให้ง่ายขึ้น
    public float rotationStep = 15f;
    public float rotationTolerance = 15f; // ค่าใหม่สำหรับยอมรับความผิดพลาดในมุม

    [Header("Debug Settings")]
    public bool showDebugInfo = true; // เพิ่ม flag สำหรับ debug

    [Header("Status")]
    public bool isPositionCorrect = false;
    public bool isRotationCorrect = false;
    public bool isGlued = false;

    private Vector2 originalPosition;
    private bool isDragging = false;
    private Camera mainCamera;
    private bool canCheckCorrectness = false; // เพิ่มการควบคุมการเช็ค

    void Start()
    {
        mainCamera = Camera.main;

        // เก็บตำแหน่งเริ่มต้นที่แท้จริง
        originalPosition = transform.position;

        // รอสักครู่ให้ PuzzleManager ทำงานเสร็จก่อน
        Invoke("EnableCorrectnessCheck", 0.1f);
    }

    //private void OnEnable()
    //{
    //    // อัปเดตตำแหน่งเริ่มต้นเมื่อ enable
    //    originalPosition = transform.position;
    //    canCheckCorrectness = true;
    //    CheckCorrectness(); // เช็คครั้งแรกหลังจากเปิดใช้งาน
    //}

    void EnableCorrectnessCheck()
    {
        canCheckCorrectness = true;
        CheckCorrectness(); // เช็คครั้งแรกหลังจากเปิดใช้งาน
    }

    void Update()
    {
        if (!isGlued && canCheckCorrectness)
        {
            CheckCorrectness();
        }
    }

    void OnMouseDown()
    {
        if (!isGlued)
        {
            isDragging = true;
        }
    }

    void OnMouseDrag()
    {
 
                // ถ้าเป็น World Space GameObject
                Vector3 mousePos3D = mainCamera.ScreenToWorldPoint(Input.mousePosition);
                mousePos3D.z = transform.position.z;
                transform.position = mousePos3D;

                if (showDebugInfo)
                {
                    Debug.Log($"[{name}] World Dragging to: ({mousePos3D.x:F3}, {mousePos3D.y:F3}, {mousePos3D.z:F3})");
                }
    }

    void OnMouseUp()
    {
        isDragging = false;
    }

    public void RotatePiece()
    {
        if (!isGlued)
        {
            transform.Rotate(0, 0, rotationStep);
        }
    }

    public void GluePiece()
    {
        isGlued = true;

        // ✅ ใช้ตำแหน่งที่คำนวณแบบเดียวกับ CheckCorrectness()
        RectTransform rectTransform = GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            // ถ้าเป็น UI Element
            rectTransform.anchoredPosition = correctPosition;
            Debug.Log($"UI Piece {name} glued at anchoredPosition ({correctPosition.x:F3}, {correctPosition.y:F3})");
        }
        else
        {
            // ถ้าเป็น World Space GameObject
            Vector3 targetWorldPos;

            if (transform.parent != null)
            {
                // แปลงจาก local coordinate เป็น world coordinate
                targetWorldPos = transform.parent.TransformPoint(new Vector3(correctPosition.x, correctPosition.y, transform.position.z));
                Debug.Log($"World Piece {name} - Local target: ({correctPosition.x:F3}, {correctPosition.y:F3}) -> World target: ({targetWorldPos.x:F3}, {targetWorldPos.y:F3})");
            }
            else
            {
                targetWorldPos = new Vector3(correctPosition.x, correctPosition.y, transform.position.z);
            }

            transform.position = targetWorldPos;
            Debug.Log($"Piece {name} glued at world position ({targetWorldPos.x:F3}, {targetWorldPos.y:F3})");
        }

        transform.rotation = Quaternion.Euler(0, 0, correctRotation);
    }

    void CheckCorrectness()
    {
        Vector2 currentPosition;

            // ถ้าเป็น World Space GameObject
            Vector3 worldPos = transform.position;

            // ถ้ามี Parent ให้ใช้ position relative to parent
            if (transform.parent != null)
            {
                worldPos = transform.parent.InverseTransformPoint(transform.position);
                //if (showDebugInfo)
                //{
                //    Debug.Log($"[{name}] Using local position relative to parent: ({worldPos.x:F3}, {worldPos.y:F3})");
                //}
            }

            currentPosition = new Vector2(worldPos.x, worldPos.y);


        float distance = Vector2.Distance(currentPosition, correctPosition);

        bool wasPositionCorrect = isPositionCorrect;
        isPositionCorrect = distance <= snapDistance;

        // ตรวจสอบการหมุน - ยอมรับความผิดพลาดในมุม
        float currentRotation = transform.eulerAngles.z;
        // Normalize angles to 0-360
        while (currentRotation < 0) currentRotation += 360;
        while (currentRotation >= 360) currentRotation -= 360;

        float normalizedCorrectRotation = correctRotation;
        while (normalizedCorrectRotation < 0) normalizedCorrectRotation += 360;
        while (normalizedCorrectRotation >= 360) normalizedCorrectRotation -= 360;

        float angleDifference = Mathf.Abs(Mathf.DeltaAngle(currentRotation, normalizedCorrectRotation));
        bool wasRotationCorrect = isRotationCorrect;
        isRotationCorrect = angleDifference <= rotationTolerance;

        if (showDebugInfo && (isDragging || wasPositionCorrect != isPositionCorrect || wasRotationCorrect != isRotationCorrect))
        {
            Debug.Log($"[{name}] Transform.pos: ({transform.position.x:F3},{transform.position.y:F3},{transform.position.z:F3}) " +
                     $"Current2D: ({currentPosition.x:F3},{currentPosition.y:F3}) -> Target: ({correctPosition.x:F3},{correctPosition.y:F3}) " +
                     $"Dist: {distance:F3} <= {snapDistance} = {isPositionCorrect} | " +
                     $"Rot: {currentRotation:F1}° -> {normalizedCorrectRotation:F1}° " +
                     $"Diff: {angleDifference:F1}° <= {rotationTolerance}° = {isRotationCorrect}");
        }
    }

    // เพิ่ม method สำหรับให้ PuzzleManager เรียกใช้บังคับเช็คตำแหน่ง
    public void ForceCheckCorrectness()
    {
        if (canCheckCorrectness)
        {
            CheckCorrectness();
        }
    }

    #region Debugging Methods
    [ContextMenu("Debug Piece Status")]
    public void DebugPieceStatus()
    {
        Vector3 currentPos3D = transform.position;
        Vector2 currentPos = new Vector2(currentPos3D.x, currentPos3D.y);
        float distance = Vector2.Distance(currentPos, correctPosition);
        float angleDifference = Mathf.Abs(Mathf.DeltaAngle(transform.eulerAngles.z, correctRotation));

        Debug.Log($"=== {name} Debug Info ===");
        Debug.Log($"Original Position: ({originalPosition.x:F3}, {originalPosition.y:F3})");
        Debug.Log($"Transform Position (3D): ({transform.position.x:F3}, {transform.position.y:F3}, {transform.position.z:F3})");
        Debug.Log($"Current Position (2D): ({currentPos.x:F3}, {currentPos.y:F3})");
        Debug.Log($"Correct Position: ({correctPosition.x:F3}, {correctPosition.y:F3})");
        Debug.Log($"Distance from Current to Correct: {distance:F3} (Snap Distance: {snapDistance})");
        Debug.Log($"Distance Check: {distance:F3} <= {snapDistance} = {distance <= snapDistance}");
        Debug.Log($"Current Rotation: {transform.eulerAngles.z:F2}°");
        Debug.Log($"Correct Rotation: {correctRotation:F2}°");
        Debug.Log($"Angle Difference: {angleDifference:F2}° (Tolerance: {rotationTolerance}°)");
        Debug.Log($"Rotation Check: {angleDifference:F2} <= {rotationTolerance} = {angleDifference <= rotationTolerance}");
        Debug.Log($"Position Correct: {isPositionCorrect}");
        Debug.Log($"Rotation Correct: {isRotationCorrect}");
        Debug.Log($"Is Glued: {isGlued}");
        Debug.Log($"Can Check Correctness: {canCheckCorrectness}");

        // เช็คว่ามี Parent หรือไม่
        if (transform.parent != null)
        {
            Debug.Log($"Parent: {transform.parent.name} at ({transform.parent.position.x:F3}, {transform.parent.position.y:F3}, {transform.parent.position.z:F3})");
            Debug.Log($"Local Position: ({transform.localPosition.x:F3}, {transform.localPosition.y:F3}, {transform.localPosition.z:F3})");
        }
        else
        {
            Debug.Log("No Parent (World Space)");
        }

        // เช็ค Canvas หรือ UI components
        var canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            Debug.Log($"Canvas found: {canvas.name}, Render Mode: {canvas.renderMode}");
        }

        // บังคับเช็คใหม่
        CheckCorrectness();
        Debug.Log($"After force check - Position: {isPositionCorrect}, Rotation: {isRotationCorrect}");
    }

    // เพิ่ม method สำหรับบังคับให้ถูกต้อง (สำหรับ debug)
    [ContextMenu("Force Correct")]
    public void ForceCorrect()
    {
        RectTransform rectTransform = GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            rectTransform.anchoredPosition = correctPosition;
        }
        else
        {
            transform.position = new Vector3(correctPosition.x, correctPosition.y, transform.position.z);
        }

        transform.rotation = Quaternion.Euler(0, 0, correctRotation);
        CheckCorrectness();
        Debug.Log($"Forced to correct position. isPositionCorrect: {isPositionCorrect}, isRotationCorrect: {isRotationCorrect}");
    }
    #endregion

   

    //// ✅ เพิ่ม method สำหรับอัปเดตตำแหน่ง correctPosition ให้ตรงกับตำแหน่งปัจจุบัน
    //[ContextMenu("Set Current Position As Correct")]
    //public void SetCurrentPositionAsCorrect()
    //{
    //    RectTransform rectTransform = GetComponent<RectTransform>();
    //    if (rectTransform != null)
    //    {
    //        correctPosition = rectTransform.anchoredPosition;
    //        Debug.Log($"Updated correct position (UI) to: ({correctPosition.x:F3}, {correctPosition.y:F3})");
    //    }
    //    else
    //    {
    //        if (transform.parent != null)
    //        {
    //            // แปลงจาก world coordinate เป็น local coordinate
    //            Vector3 localPos = transform.parent.InverseTransformPoint(transform.position);
    //            correctPosition = new Vector2(localPos.x, localPos.y);
    //            Debug.Log($"Updated correct position (Local) to: ({correctPosition.x:F3}, {correctPosition.y:F3}) from world ({transform.position.x:F3}, {transform.position.y:F3})");
    //        }
    //        else
    //        {
    //            correctPosition = new Vector2(transform.position.x, transform.position.y);
    //            Debug.Log($"Updated correct position (World) to: ({correctPosition.x:F3}, {correctPosition.y:F3})");
    //        }
    //    }

    //    correctRotation = transform.eulerAngles.z;
    //    Debug.Log($"Updated correct rotation to: {correctRotation:F1}°");
    //    CheckCorrectness();
    //}

    //// ✅ เพิ่ม method สำหรับอัปเดตตำแหน่งเริ่มต้น (ถ้าจำเป็น)
    //[ContextMenu("Update Original Position")]
    //public void UpdateOriginalPosition()
    //{
    //    originalPosition = transform.position;
    //    Debug.Log($"Updated original position to: ({originalPosition.x:F2}, {originalPosition.y:F2})");
    //}
}