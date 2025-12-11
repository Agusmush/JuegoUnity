using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;

public class InputManager : NetworkBehaviour
{
    private PlayerInput playerInput;
    private PlayerInput.OnFootActions onFoot;

    private PlayerMotor motor;
    private PlayerLook look;
    private PlayerInteraction interaction;
    private BombCarrier carrier;
    private PlayerHealth health;

    public override void OnNetworkSpawn()
    {
        // Si no soy el dueño, me apago para no controlar a otros
        if (!IsOwner)
        {
            this.enabled = false;
            return;
        }
    }

    void Awake()
    {
        playerInput = new PlayerInput();
        onFoot = playerInput.OnFoot;

        motor = GetComponent<PlayerMotor>();
        look = GetComponent<PlayerLook>();
        health = GetComponent<PlayerHealth>();
        interaction = GetComponent<PlayerInteraction>();
        carrier = GetComponent<BombCarrier>();

        onFoot.Jump.performed += ctx => motor.Jump();

        if (interaction != null)
            onFoot.Kick.performed += ctx => interaction.OnKickInput();

        if (carrier != null)
            onFoot.Interact.performed += ctx => carrier.OnGrabInput();

        if (health != null)
            onFoot.DebugKill.performed += ctx => health.OnDebugKill();

        onFoot.Crouch.performed += ctx => motor.SetCrouch(true);
        onFoot.Crouch.canceled += ctx => motor.SetCrouch(false);
    }

    void FixedUpdate()
    {
        if (!IsOwner) return;
        motor.ProcessMove(onFoot.Movement.ReadValue<Vector2>());
    }

    private void LateUpdate()
    {
        if (!IsOwner) return;
        look.ProcessLook(onFoot.Look.ReadValue<Vector2>());
    }

    private void OnEnable()
    {
        onFoot.Enable();
    }

    private void OnDisable()
    {
        onFoot.Disable();
    }
}