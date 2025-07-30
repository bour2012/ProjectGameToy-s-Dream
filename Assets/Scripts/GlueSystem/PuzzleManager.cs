using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class PuzzleManager : MonoBehaviour
{
    [Header("Puzzle Pieces")]
    public List<PuzzlePiece> puzzlePieces = new List<PuzzlePiece>();

    [Header("UI Elements")]
    public Button glueButton;
    public Button rotateButton;
    public TextMeshProUGUI statusText;

    [Header("Settings")]
    public GameObject glueEffectPrefab;

    [Header("Positioning")]
    public Transform targetCenter;  // ตำแหน่งที่ต้องการให้ Puzzle อยู่

    [Header("Repair System Connection")]
    public DollRepairSystem dollRepairSystem; // เชื่อมต่อกับ DollRepairSystem

    private PuzzlePiece selectedPiece;
    private bool allPiecesCorrect = false;
    private bool puzzleCompleted = false; // เพิ่มการติดตาม state
    private bool isInitialized = false; // เพิ่มการตรวจสอบว่า Initialize เสร็จแล้ว

    // Events
    public System.Action OnPuzzleSystemCompleted; // Event สำหรับแจ้งระบบอื่น

    private void OnEnable()
    {
        CenterPuzzlePieces();
        isInitialized = true;
       // StartCoroutine(InitializePuzzle()); // เปลี่ยนเป็น Coroutine
    }

    // เพิ่ม Coroutine สำหรับ Initialize
    IEnumerator InitializePuzzle()
    {
        isInitialized = false;

        // รอ 1 frame ให้ Start() ของ PuzzlePiece ทำงานก่อน
        yield return null;

        CenterPuzzlePieces(); // ขยับตำแหน่งชิ้นส่วนทั้งหมด

        // รออีก 1 frame ให้ตำแหน่งอัพเดต
        yield return null;

        isInitialized = true;
        Debug.Log("Puzzle initialized successfully");
    }

    void Start()
    {
        SetupButtons();
        UpdateUI();

        // หา DollRepairSystem อัตโนมัติถ้าไม่ได้กำหนด
        if (dollRepairSystem == null)
        {
            dollRepairSystem = GetComponentInParent<DollRepairSystem>();
            if (dollRepairSystem == null)
            {
                dollRepairSystem = FindFirstObjectByType<DollRepairSystem>();
            }
        }
    }

    void Update()
    {
        // รอให้ Initialize เสร็จก่อน
        if (!isInitialized) return;

        if (!puzzleCompleted)
        {
            CheckPuzzleCompletion();
            UpdateUI();
            HandleInput();
        }
    }

    void SetupButtons()
    {
        if (glueButton != null)
            glueButton.onClick.AddListener(UseGlue);

        if (rotateButton != null)
            rotateButton.onClick.AddListener(RotateSelectedPiece);
    }

    void HandleInput()
    {
        if (Input.GetMouseButtonDown(0))
            SelectPiece();

        if (Input.GetKeyDown(KeyCode.R))
            RotateSelectedPiece();
    }

    void SelectPiece()
    {
        Vector2 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        RaycastHit2D hit = Physics2D.Raycast(mousePos, Vector2.zero);

        if (hit.collider != null)
        {
            PuzzlePiece piece = hit.collider.GetComponent<PuzzlePiece>();
            if (piece != null && !piece.isGlued)
            {
                selectedPiece = piece;
                Debug.Log($"Selected piece: {piece.name}");
            }
        }
    }

    void RotateSelectedPiece()
    {
        if (selectedPiece != null && !puzzleCompleted)
        {
            selectedPiece.RotatePiece();
            Debug.Log($"Rotated piece: {selectedPiece.name}");
        }
    }

    void CheckPuzzleCompletion()
    {
        allPiecesCorrect = true;

        foreach (PuzzlePiece piece in puzzlePieces)
        {
            if (!piece.isPositionCorrect || !piece.isRotationCorrect)
            {
                allPiecesCorrect = false;
                break;
            }
        }
    }

    void UpdateUI()
    {
        if (glueButton != null)
            glueButton.interactable = allPiecesCorrect && !puzzleCompleted;

        if (statusText != null)
        {
            if (puzzleCompleted)
            {
                statusText.text = "ปริศนาแก้เสร็จแล้ว!";
                statusText.color = Color.blue;
            }
            else if (allPiecesCorrect)
            {
                statusText.text = "พร้อมใช้กาว!";
                statusText.color = Color.green;
            }
            else
            {
                statusText.text = "จัดชิ้นส่วนให้ถูกต้อง";
                statusText.color = Color.red;
            }
        }
    }

    public void UseGlue()
    {
        if (allPiecesCorrect && !puzzleCompleted)
        {
            StartCoroutine(GlueSequence());
        }
    }

    IEnumerator GlueSequence()
    {
        // ปิดการใช้งานปุ่มชั่วคราว
        if (glueButton != null) glueButton.interactable = false;

        // วางกาวแต่ละชิ้น
        foreach (PuzzlePiece piece in puzzlePieces)
        {
            piece.GluePiece();

            // เอฟเฟกต์กาว
            if (glueEffectPrefab != null)
            {
                GameObject effect = Instantiate(glueEffectPrefab, piece.transform.position, Quaternion.identity);
                Destroy(effect, 2f);
            }

            // รอสักครู่ระหว่างแต่ละชิ้น
            yield return new WaitForSeconds(0.3f);
        }

        // รอให้เอฟเฟกต์เล่นเสร็จ
        yield return new WaitForSeconds(0.5f);

        // ปริศนาเสร็จสมบูรณ์
        puzzleCompleted = true;
        OnPuzzleComplete();
    }

    void OnPuzzleComplete()
    {
        Debug.Log("Puzzle Complete!");

        // อัพเดต UI
        UpdateUI();

        // เรียก Event สำหรับระบบอื่นๆ
        OnPuzzleSystemCompleted?.Invoke();

        // แจ้งไปยัง DollRepairSystem
        NotifyRepairSystemCompleted();
    }

    /// <summary>
    /// แจ้งไปยัง DollRepairSystem ว่าระบบปริศนาเสร็จสมบูรณ์แล้ว
    /// </summary>
    void NotifyRepairSystemCompleted()
    {
        if (dollRepairSystem != null)
        {
            dollRepairSystem.OnSubSystemCompleted("Glue");
        }
        else
        {
            Debug.LogWarning("DollRepairSystem not found! Cannot notify puzzle completion.");
        }
    }

    /// <summary>
    /// จัดตำแหน่ง PuzzlePieces ให้อยู่กลางจอ โดยไม่เปลี่ยน Z
    /// </summary>
    private void CenterPuzzlePieces()
    {
        if (puzzlePieces == null || puzzlePieces.Count == 0) return;
        if (targetCenter == null)
        {
            Debug.LogWarning("targetCenter ยังไม่ได้เซ็ตใน Inspector");
            return;
        }

        // คำนวณ center ปัจจุบันของชิ้นส่วน
        Vector3 currentCenter = Vector3.zero;
        int count = 0;

        foreach (PuzzlePiece piece in puzzlePieces)
        {
            if (piece != null)
            {
                currentCenter += piece.transform.position;
                count++;
            }
        }

        if (count == 0) return;
        currentCenter /= count;

        // คำนวณ offset
        Vector3 offset = targetCenter.position - currentCenter;

        // ขยับแต่ละชิ้นโดยไม่เปลี่ยนค่า Z และอัปเดต correctPosition ด้วย
        foreach (PuzzlePiece piece in puzzlePieces)
        {
            if (piece != null)
            {
                Vector3 newPos = piece.transform.position + offset;
                newPos.z = piece.transform.position.z;
                piece.transform.position = newPos;

                // อัปเดตตำแหน่งที่ถูกต้อง (correctPosition) ให้เลื่อนตามไปด้วย
                piece.correctPosition += new Vector2(offset.x, offset.y);

                // DEBUG: แสดงตำแหน่งใหม่ของ correctPosition
                Debug.Log($"[DEBUG] '{piece.name}' correctPosition updated to: {piece.correctPosition}");

                // บังคับให้ PuzzlePiece เช็คตำแหน่งใหม่ทันที
                piece.ForceCheckCorrectness();
            }
        }

        Debug.Log($"Puzzle pieces centered with offset: {offset}");
    }

    // เพิ่ม method สำหรับ debug
    [ContextMenu("Debug Puzzle Positions")]
    public void DebugPuzzlePositions()
    {
        foreach (PuzzlePiece piece in puzzlePieces)
        {
            if (piece != null)
            {
                float distance = Vector2.Distance(piece.transform.position, piece.correctPosition);
                Debug.Log($"Piece {piece.name}: Current({piece.transform.position.x:F2}, {piece.transform.position.y:F2}) " +
                         $"Correct({piece.correctPosition.x:F2}, {piece.correctPosition.y:F2}) " +
                         $"Distance: {distance:F2} " +
                         $"Position Correct: {piece.isPositionCorrect} " +
                         $"Rotation Correct: {piece.isRotationCorrect}");
            }
        }
    }
}