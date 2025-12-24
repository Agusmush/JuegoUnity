using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerMotor : MonoBehaviour
{
    public CharacterController controller;

    [Header("Físicas y Reacción")]
    public float mass = 70f;
    public float impactRecoverySpeed = 5f;

    // Parámetros de movimiento
    public float groundAccel = 12f;
    public float airAccel = 2f;
    public float maxSpeed = 7f;
    public float airMaxSpeed = 11f;
    public float friction = 8f;
    public float gravity = -30.0f;
    public float jumpForce = 8.0f;

    public float crouchHeight = 1.2f;
    public float crouchSpeed = 2f;
    public float normalHeight = 2f;
    public float jumpHeight = 3.0f;
    public float airStraffleSmooth = 0.15f;

    // --- NUEVO: Factores de Peso ---
    [HideInInspector] public float speedFactor = 1f;
    [HideInInspector] public float jumpFactor = 1f;

    private Vector3 velocity;
    private bool isGrounded;
    private bool isCrouching;
    private PlayerAudio playerAudio;

    private Transform camHolder;
    private float impactLockoutTimer = 0f;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        if (Camera.main != null) camHolder = Camera.main.transform;

        // Aseguramos que los factores empiecen en 1 (normal)
        speedFactor = 1f;
        jumpFactor = 1f;
        playerAudio = GetComponent<PlayerAudio>();
    }

    void Update()
    {
        if (controller == null) return;

        isGrounded = controller.isGrounded;

        bool underImpact = impactLockoutTimer > 0;
        if (underImpact) impactLockoutTimer -= Time.deltaTime;

        if (isGrounded && !underImpact)
        {
            ApplyFriction();
        }

        HandleCrouch();
    }

    // Esta función la llama el InputManager
    public void ProcessMove(Vector2 input)
    {
        if (!isActiveAndEnabled) return;
        if (controller == null || !controller.enabled) return;

        // Si camHolder es nulo, buscamos la cámara
        if (camHolder == null) camHolder = Camera.main?.transform;

        Vector3 wishDir = Vector3.zero;
        if (camHolder != null)
        {
            wishDir = camHolder.TransformDirection(new Vector3(input.x, 0, input.y));
        }
        else
        {
            wishDir = transform.TransformDirection(new Vector3(input.x, 0, input.y));
        }

        wishDir.y = 0;
        wishDir.Normalize();

        if (isGrounded)
        {
            GroundMove(wishDir, input.magnitude);
        }
        else
        {
            AirMove(wishDir, input.magnitude);
        }

        velocity.y += gravity * Time.deltaTime;

        controller.Move(velocity * Time.deltaTime);

        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }
    }

    void GroundMove(Vector3 wishDir, float inputMag)
    {
        if (impactLockoutTimer > 0) return;

        float curMaxSpeed = isCrouching ? crouchSpeed : maxSpeed;

        // --- APLICAMOS SPEED FACTOR (PESO) ---
        curMaxSpeed *= speedFactor;

        Accelerate(wishDir, curMaxSpeed, groundAccel, inputMag);
    }

    void AirMove(Vector3 wishDir, float inputMag)
    {
        Accelerate(wishDir, airMaxSpeed, airAccel, inputMag);
    }

    void Accelerate(Vector3 wishDir, float maxSpeed, float accel, float wishSpeedScale)
    {
        float wishSpeed = maxSpeed * Mathf.Clamp01(wishSpeedScale);
        float currentSpeed = Vector3.Dot(velocity, wishDir);
        float addSpeed = wishSpeed - currentSpeed;
        if (addSpeed <= 0) return;

        float accelSpeed = accel * Time.deltaTime;
        if (accelSpeed > addSpeed) accelSpeed = addSpeed;

        velocity += wishDir.normalized * accelSpeed;
    }

    void ApplyFriction()
    {
        Vector3 lateralVel = velocity;
        lateralVel.y = 0;
        float speed = lateralVel.magnitude;

        if (speed < 0.1f)
        {
            velocity.x = 0;
            velocity.z = 0;
            return;
        }

        float drop = speed * friction * Time.deltaTime;
        float newSpeed = Mathf.Max(speed - drop, 0);

        if (speed > 0)
        {
            velocity.x *= newSpeed / speed;
            velocity.z *= newSpeed / speed;
        }
    }

    private void HandleCrouch()
    {
        float targetHeight = isCrouching ? crouchHeight : normalHeight;
        controller.height = Mathf.Lerp(controller.height, targetHeight, Time.deltaTime * 12f);
    }

    public void SetCrouch(bool value)
    {
        isCrouching = value;
    }

    public void Jump()
    {
        // --- APLICAMOS JUMP FACTOR (PESO) ---
        if (isGrounded && impactLockoutTimer <= 0)
        {
            float finalJumpHeight = jumpHeight * jumpFactor;
            velocity.y = Mathf.Sqrt(finalJumpHeight * -2f * gravity);
            if (playerAudio != null) playerAudio.PlayJumpSound();
        }
    }

    // --- FUNCIONES DE EXPLOSIÓN ---

    public void ApplyKnockback(Vector3 explosionPos, float explosionForce, float explosionRadius)
    {
        Vector3 direction = transform.position - explosionPos;
        float distance = direction.magnitude;
        float impactFactor = 1 - (distance / explosionRadius);
        impactFactor = Mathf.Clamp01(impactFactor);

        if (impactFactor > 0)
        {
            direction.Normalize();
            direction += Vector3.up * 0.5f;
            direction.Normalize();

            AddExplosionForce(direction, explosionForce * impactFactor);
        }
    }

    public void AddExplosionForce(Vector3 direction, float force)
    {
        direction.Normalize();
        velocity += direction * (force / mass);

        if (force > 5f)
        {
            impactLockoutTimer = 0.5f;
        }
    }

    public void ResetMotion()
    {
        velocity = Vector3.zero;
        impactLockoutTimer = 0f;
        isCrouching = false;

        // Reseteamos también los factores de peso
        speedFactor = 1f;
        jumpFactor = 1f;
    }
}