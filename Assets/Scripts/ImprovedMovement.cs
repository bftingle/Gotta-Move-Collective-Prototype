﻿using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public class ImprovedMovement : Movement {
    private Collision coll;
    [HideInInspector]
    public Rigidbody2D rb;
    private AnimationScript anim;

    [Space]
    [Header("Stats")]
    public float speed = 10;
    public float jumpForce = 50;
    public float slideSpeed = 5;
    public float wallJumpLerp = 10;
    public float dashSpeed = 20;
    public float terminalVelocity = 10;

    [Space]
    [Header("Booleans")]
    //public bool canMove;
    //public bool wallGrab;
    //public bool wallJumped;
    //public bool wallSlide;
    //public bool isDashing;
    private bool groundTouch;
    private bool hasDashed;

    private int side = 1;
    private Color sColor;

    //Saved directional variables for consistent dashing
    private float xRawSaved = 1.0f;
    private float yRawSaved = 0.0f;

    //Buffers for Coyote Time and Jumping
    private int coyoteBuffer = 0;
    private int jumpBuffer = 0;

    //Saved X for walk response
    private float prevXInput = 0;

    //Used for terminal velocity
    private int vTemp = 1;

    [Space]
    [Header("Polish")]
    public ParticleSystem dashParticle;
    public ParticleSystem jumpParticle;
    public ParticleSystem wallJumpParticle;
    public ParticleSystem slideParticle;

    SpriteRenderer sprite;

    // Start is called before the first frame update
    void Start() {
        coll = GetComponent<Collision>();
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponentInChildren<AnimationScript>();
        sprite = GetComponentInChildren<SpriteRenderer>();
        sColor = new Color(0.5f, 0.5f, 1f, 1f);
        sprite.color = Color.white;
    }

    // Update is called once per frame
    void Update() {
        float x = Input.GetAxis("Horizontal");
        float y = Input.GetAxis("Vertical");
        float xRaw = Input.GetAxisRaw("Horizontal");
        float yRaw = Input.GetAxisRaw("Vertical");
        //Saving non-zero movement
        if (xRaw != 0 || yRaw != 0) {
            xRawSaved = xRaw;
            yRawSaved = yRaw;
        }
        Vector2 dir = new Vector2(x, y);

        Walk(dir);
        anim.SetHorizontalMovement(x, y, rb.velocity.y);

        if (coll.onWall && Input.GetButton("Fire1") && canMove) {
            if (side != coll.wallSide)
                anim.Flip(side * -1);
            wallGrab = true;
            wallSlide = false;
        }

        if (Input.GetButtonUp("Fire1") || !coll.onWall || !canMove) {
            wallGrab = false;
            wallSlide = false;
        }

        if (coll.onGround && !isDashing) {
            wallJumped = false;
            GetComponent<BetterJumping>().enabled = true;
        }

        //Jump Buffer Decrement
        if (jumpBuffer > -1) {
            jumpBuffer--;
        }

        //Coyote Time and Jump usage
        if (coll.onGround) {
            coyoteBuffer = 12;
            if (jumpBuffer > -1) {
                jumpBuffer = -1;
                Jump(Vector2.up, false);
            }
        } else {
            coyoteBuffer--;
        }

        if (wallGrab && !isDashing) {
            rb.gravityScale = 0;
            if (x > .2f || x < -.2f)
                rb.velocity = new Vector2(rb.velocity.x, 0);

            float speedModifier = y > 0 ? .5f : 1;

            rb.velocity = new Vector2(rb.velocity.x, y * (speed * speedModifier));
        }
        else {
            //Feels Better
            rb.gravityScale = 4;
        }

        if (coll.onWall && !coll.onGround) {
            if (x != 0 && !wallGrab) {
                wallSlide = true;
                WallSlide();
            }
        }

        if (!coll.onWall || coll.onGround)
            wallSlide = false;

        if (Input.GetButtonDown("Jump")) {
            anim.SetTrigger("jump");

            //Coyote Time allowance and Jump Buffer setting
            if (coyoteBuffer > -1)
                Jump(Vector2.up, false);
            else if (coll.onWall)
                WallJump();
            else
                jumpBuffer = 12;
        }

        //Consistent Dash
        if (Input.GetButtonDown("Fire2") && !hasDashed) {
            Dash(xRawSaved, yRawSaved);
        }

        if (coll.onGround && !groundTouch) {
            GroundTouch();
            groundTouch = true;
        }

        if (!coll.onGround && groundTouch) {
            groundTouch = false;
        }

        //Terminal Velocity
        if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
        {
            vTemp = 2;
        } else
        if (Input.GetKeyUp(KeyCode.DownArrow) || Input.GetKeyUp(KeyCode.S))
        {
            vTemp = 1;
        }
        if(rb.velocity.y < (terminalVelocity * vTemp * -1) && !coll.onWall)
        {
            rb.velocity = new Vector2(rb.velocity.x, terminalVelocity * vTemp * -1);
        }
        WallParticle(y);

        if (wallGrab || wallSlide || !canMove)
            return;

        if (x > 0) {
            side = 1;
            anim.Flip(side);
        }
        if (x < 0) {
            side = -1;
            anim.Flip(side);
        }

        //Saving
        prevXInput = dir.x;
    }

    void GroundTouch() {
        hasDashed = false;
        sprite.color = Color.white;
        isDashing = false;

        side = anim.sr.flipX ? -1 : 1;

        jumpParticle.Play();
    }

    private void Dash(float x, float y) {
        Camera.main.transform.DOComplete();
        Camera.main.transform.DOShakePosition(.2f, .5f, 14, 90, false, true);
        FindObjectOfType<RippleEffect>().Emit(Camera.main.WorldToViewportPoint(transform.position));

        hasDashed = true;
        sprite.color = sColor;

        anim.SetTrigger("dash");
        //Momentum Holder
        var holder = Math.Abs(((int)rb.velocity.x));
        if (holder < 5) {
            holder = 1;
        }
        else {
            holder = 2;
        }

        // New Dash
        Vector2 dir = new Vector2(x, y);
        float speedX = dir.normalized.x * dashSpeed * holder;
        float speedY = dir.normalized.y * dashSpeed * holder;
        Vector2 speed = new Vector2(speedX, speedY);
        rb.velocity = speed;

        StartCoroutine(DashWait());
    }

    IEnumerator DashWait() {
        FindObjectOfType<GhostTrail>().ShowGhost();
        StartCoroutine(GroundDash());
        DOVirtual.Float(14, 0, .8f, RigidbodyDrag);

        dashParticle.Play();
        rb.gravityScale = 0;
        GetComponent<BetterJumping>().enabled = false;
        wallJumped = true;
        isDashing = true;

        yield return new WaitForSeconds(.3f);

        dashParticle.Stop();
        rb.gravityScale = 3;
        GetComponent<BetterJumping>().enabled = true;
        wallJumped = false;
        isDashing = false;
    }

    IEnumerator GroundDash() {
        yield return new WaitForSeconds(.15f);
        if (coll.onGround)
        {
            hasDashed = false;
            sprite.color = Color.white;
        }
    }

    private void WallJump() {
        if ((side == 1 && coll.onRightWall) || side == -1 && !coll.onRightWall) {
            side *= -1;
            anim.Flip(side);
        }

        StopCoroutine(DisableMovement(0));
        StartCoroutine(DisableMovement(.1f));

        Vector2 wallDir = coll.onRightWall ? Vector2.left : Vector2.right;

        Jump((Vector2.up / 1.5f + wallDir / 1.5f), true);

        wallJumped = true;
    }

    private void WallSlide() {
        if (coll.wallSide != side)
            anim.Flip(side * -1);

        if (!canMove)
            return;

        bool pushingWall = false;
        if ((rb.velocity.x > 0 && coll.onRightWall) || (rb.velocity.x < 0 && coll.onLeftWall)) {
            pushingWall = true;
        }
        float push = pushingWall ? 0 : rb.velocity.x;

        //Walls decelerate like gravity instead of instantly moving the player down
        float newYVeloc = rb.velocity.y - (slideSpeed/144);
        if (newYVeloc < -slideSpeed) newYVeloc = -slideSpeed;
        rb.velocity = new Vector2(push, newYVeloc);
    }

    private void Walk(Vector2 dir) {
        if (!canMove)
            return;

        if (wallGrab)
            return;

        if (!wallJumped) {
            //increase and decrease the players velocity quicker to make the movement more responsive
            if ((Math.Abs(dir.x) < .90 && Math.Abs(dir.x) < Math.Abs(prevXInput)) || coll.onWall) {
                if ((coll.onRightWall && dir.x > 0) || (coll.onLeftWall && dir.x < 0) || !coll.onWall)
                    rb.velocity = new Vector2(0, rb.velocity.y);
                else
                    rb.velocity = new Vector2(dir.x * speed, rb.velocity.y);
            }

            else {
                if (Math.Abs(dir.x) < .4 && dir.x != 0)
                    rb.velocity = new Vector2((float)(dir.x / Math.Abs(dir.x) * .4 * speed), rb.velocity.y);
                else if (Math.Abs(dir.x) < .7 && dir.x != 0)
                    rb.velocity = new Vector2((float)(dir.x / Math.Abs(dir.x) * .7 * speed), rb.velocity.y);
                else
                    rb.velocity = new Vector2(dir.x * speed, rb.velocity.y);
            }
        } else {
            rb.velocity = Vector2.Lerp(rb.velocity, (new Vector2(dir.x * speed, rb.velocity.y)), wallJumpLerp * Time.deltaTime);
        }
    }

    private void Jump(Vector2 dir, bool wall) {
        slideParticle.transform.parent.localScale = new Vector3(ParticleSide(), 1, 1);
        ParticleSystem particle = wall ? wallJumpParticle : jumpParticle;

        rb.velocity = new Vector2(rb.velocity.x, 0);
        rb.velocity += dir * jumpForce;

        particle.Play();
    }

    IEnumerator DisableMovement(float time) {
        canMove = false;
        yield return new WaitForSeconds(time);
        canMove = true;
    }

    void RigidbodyDrag(float x) {
        rb.drag = x;
    }

    void WallParticle(float vertical) {
        var main = slideParticle.main;

        if (wallSlide || (wallGrab && vertical < 0)) {
            slideParticle.transform.parent.localScale = new Vector3(ParticleSide(), 1, 1);
            main.startColor = Color.white;
        }
        else {
            main.startColor = Color.clear;
        }
    }

    int ParticleSide() {
        int particleSide = coll.onRightWall ? 1 : -1;
        return particleSide;
    }
}
