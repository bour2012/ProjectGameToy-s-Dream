﻿using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    public float speed = 1f;
    public float jumpSpeed = 3f;
    public bool groundCheck;
    public bool isSwinging;
    private SpriteRenderer playerSprite;
    private Rigidbody2D rBody;
    private bool isJumping;
    private Animator animator;
    private float jumpInput;
    private float horizontalInput;
    public Vector2 ropeHook;
    public float swingForce = 4f;
    float rayLength = 0.1f;
    public LayerMask groundLayer;

    void Awake()
    {
        playerSprite = GetComponent<SpriteRenderer>();
        rBody = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
    }

    void Update()
    {
        jumpInput = Input.GetAxis("Jump");
        horizontalInput = Input.GetAxis("Horizontal");
        var halfHeight = transform.GetComponent<SpriteRenderer>().bounds.extents.y;

        groundCheck = Physics2D.Raycast(
            new Vector2(transform.position.x, transform.position.y - halfHeight),
            Vector2.down,
            rayLength,
            groundLayer
        );
    }

    void FixedUpdate()
    {
        if (horizontalInput < 0f || horizontalInput > 0f)
        {
            animator.SetFloat("Speed", Mathf.Abs(horizontalInput));
            playerSprite.flipX = horizontalInput < 0f;

            if (isSwinging)
            {
                animator.SetBool("IsSwinging", true);

                var playerToHookDirection = (ropeHook - (Vector2)transform.position).normalized;
                Vector2 perpendicularDirection;

                if (horizontalInput < 0)
                {
                    perpendicularDirection = new Vector2(-playerToHookDirection.y, playerToHookDirection.x);
                    var leftPerpPos = (Vector2)transform.position - perpendicularDirection * -2f;
                    Debug.DrawLine(transform.position, leftPerpPos, Color.green, 0f);
                }
                else
                {
                    perpendicularDirection = new Vector2(playerToHookDirection.y, -playerToHookDirection.x);
                    var rightPerpPos = (Vector2)transform.position + perpendicularDirection * 2f;
                    Debug.DrawLine(transform.position, rightPerpPos, Color.green, 0f);
                }

                var force = perpendicularDirection * swingForce;
                rBody.AddForce(force, ForceMode2D.Force);
            }
            else
            {
                animator.SetBool("IsSwinging", false);
                if (groundCheck)
                {
                    var groundForce = speed * 2f;
                    rBody.AddForce(new Vector2((horizontalInput * groundForce - rBody.linearVelocity.x) * groundForce, 0));
                    rBody.linearVelocity = new Vector2(rBody.linearVelocity.x, rBody.linearVelocity.y);
                }
            }
        }
        else
        {
            animator.SetBool("IsSwinging", false);
            animator.SetFloat("Speed", 0f);
        }

        if (!isSwinging)
        {
            animator.SetBool("IsSwinging", false);

            // เดินด้วย linearVelocity แนวนอน
            float moveSpeed = speed;
            rBody.linearVelocity = new Vector2(horizontalInput * moveSpeed, rBody.linearVelocity.y);

            // กระโดด ถ้าอยู่บนพื้น
            if (groundCheck && jumpInput > 0f)
            {
                rBody.linearVelocity = new Vector2(rBody.linearVelocity.x, jumpSpeed);
            }
        }
    }
}
