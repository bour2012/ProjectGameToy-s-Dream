using UnityEngine;
using System.Collections.Generic;

public class LeverPuzzleManager : MonoBehaviour
{
    [Header("Puzzle Elements")]
    public List<Lever> puzzleLevers;      // ลาก Lever ทั้งหลายมาใส่ที่นี่

    [Header("Rewards (Doors)")]
    public List<PlatformController> doors; // ลากประตูบานต่างๆมาใส่ที่นี่

    [Header("Options")]
    [Tooltip("ถ้า false จะไม่ใช้ระบบ Puzzle อัตโนมัติ (ประตูทำงานปกติ)")]
    public bool usePuzzleMode = true;

    [Header("Status")]
    public bool isSolved = false;

    void Start()
    {
        // Setup อัตโนมัติ: บอก Lever ทุกตัวว่า Manager คือฉันนะ
        foreach (var lever in puzzleLevers)
        {
            if (lever != null) lever.puzzleManager = this;
        }
    }

    public void CheckWinCondition()
    {
        if (!usePuzzleMode) return;
        if (isSolved) return; // ถ้าชนะไปแล้วไม่ต้องเช็คซ้ำ

        // 1. เช็คทุก Lever
        foreach (var lever in puzzleLevers)
        {
            if (lever == null) continue;
            // ถ้ามีอันไหนปิดอยู่ (isActive == false) ให้จบข่าว ยังไม่ชนะ
            if (!lever.isActive) return;
        }

        // 2. ถ้าหลุดลูปมาได้ แปลว่าทุกอัน Active หมดแล้ว -> ชนะ!
        PuzzleSolved();
    }

    void PuzzleSolved()
    {
        isSolved = true;
        Debug.Log("PUZZLE SOLVED! Opening all doors...");

        // สั่งเปิดประตูทุกบาน
        foreach (var door in doors)
        {
            if (door != null)
            {
                // ใช้ฟังก์ชัน Toggle ที่คุณมีอยู่แล้วใน PlatformController
                door.Toggle(true);
            }
        }
    }
}
