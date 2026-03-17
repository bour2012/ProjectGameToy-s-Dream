using UnityEngine;

public class LadderTop : MonoBehaviour
{
    [Header("Ladder Properties")]

    public bool allowClimbFromBottom = true;

    public bool allowClimbFromTop = true;





    private bool isPlayerOnLadder = false; //   Ǩ ͺ  Ҽ            ѹ        

    private Rigidbody2D playerRb;         // Reference   ѧ Rigidbody2D  ͧ      
    //private float savedGravity;


    public PlayerMovement playerMovement;

    private void Start()

    {

        //   駤   Trigger    Ѻ Collider
        //savedGravity = playerRb.gravityScale;
        Collider2D col = GetComponent<Collider2D>();

        if (col != null)

        {

            col.isTrigger = true;

        }




        gameObject.layer = 8;

    }



    private void Update()

    {

        if (isPlayerOnLadder && playerRb != null)

        {

            if (playerMovement.isClimbing)

            {

              

                if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.S))

                {

                   

                    playerRb.gravityScale = 0f;



                    float verticalInput = Input.GetAxis("Vertical");

                    playerRb.linearVelocity = new Vector2(playerRb.linearVelocity.x, verticalInput * 5f); 

                }

                else

                {
 

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



            //    Reference   ѧ Rigidbody2D  ͧ      

            playerRb = other.GetComponent<Rigidbody2D>();

            //playerController = other.GetComponent<PlayerController>();



            if (playerRb != null)

            {

                isPlayerOnLadder = true; //              ѹ 

            }

        }

    }



    private void OnTriggerExit2D(Collider2D other)

    {

        if (other.CompareTag("Player"))

        {

            //Debug.Log("Player exited ladder area");




            isPlayerOnLadder = false;

            if (playerRb != null)

            {

                playerRb.gravityScale = 2;  

            }

        }

    }
}
