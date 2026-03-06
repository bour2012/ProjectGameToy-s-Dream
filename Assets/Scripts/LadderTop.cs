using UnityEngine;

public class LadderTop : MonoBehaviour
{
    [Header("Ladder Properties")]

    public bool allowClimbFromBottom = true;

    public bool allowClimbFromTop = true;





    private bool isPlayerOnLadder = false; // ตรวจสอบว่าผู้เล่นอยู่ใกล้บันไดหรือไม่

    private Rigidbody2D playerRb;         // Reference ไปยัง Rigidbody2D ของผู้เล่น
    //private float savedGravity;


    public PlayerMovement playerMovement;

    private void Start()

    {

        // ตั้งค่า Trigger ให้กับ Collider
        //savedGravity = playerRb.gravityScale;
        Collider2D col = GetComponent<Collider2D>();

        if (col != null)

        {

            col.isTrigger = true;

        }



        // ตั้งค่า Layer เป็น Ladder Layer (8)

        gameObject.layer = 8;

    }



    private void Update()

    {

        if (isPlayerOnLadder && playerRb != null)

        {

            if (playerMovement.isClimbing)

            {

                // ตรวจสอบว่าผู้เล่นกด W หรือ S

                if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.S))

                {

                    //ปิดแรงโน้มถ่วงเพื่อให้ผู้เล่นปีนบันไดได้

                    playerRb.gravityScale = 0f;



                    // ควบคุมการเคลื่อนที่ขึ้น-ลงบันได

                    float verticalInput = Input.GetAxis("Vertical");

                    playerRb.linearVelocity = new Vector2(playerRb.linearVelocity.x, verticalInput * 5f); // ความเร็วปีนบันได

                }

                else

                {

                    // ถ้าไม่ได้กด W หรือ S ให้หยุดการเคลื่อนที่

                    playerRb.linearVelocity = new Vector2(playerRb.linearVelocity.x, 0f);

                }

            }

        }

    }



    private void OnTriggerEnter2D(Collider2D other)

    {

        if (other.CompareTag("Player"))

        {

            //Debug.Log("Player entered ladder area");



            // เก็บ Reference ไปยัง Rigidbody2D ของผู้เล่น

            playerRb = other.GetComponent<Rigidbody2D>();

            //playerController = other.GetComponent<PlayerController>();



            if (playerRb != null)

            {

                isPlayerOnLadder = true; // ผู้เล่นอยู่ใกล้บันได

            }

        }

    }



    private void OnTriggerExit2D(Collider2D other)

    {

        if (other.CompareTag("Player"))

        {

            //Debug.Log("Player exited ladder area");



            // รีเซ็ตสถานะเมื่อผู้เล่นออกจากบันได

            isPlayerOnLadder = false;

            if (playerRb != null)

            {

                playerRb.gravityScale = 2; // คืนค่าแรงโน้มถ่วงปกติ

            }

        }

    }
}
