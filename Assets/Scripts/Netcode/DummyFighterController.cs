using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TekkenNetcodeDemo
{
    /// <summary>
    /// Demonstrates Client Ownership and Input Authority:
    /// - `if (!IsOwner) return;` guarantees each client only controls their assigned character.
    /// - Movement is applied locally and replicated across the network via NetworkTransform.
    /// - Implements classic 2.5D Tekken movement: lateral stepping (X), crouching (Y), and jumping (Y).
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class DummyFighterController : NetworkBehaviour
    {
        [Header("Movement Config")]
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float jumpForce = 6f;
        [SerializeField] private float gravity = -15f;
        [SerializeField] private float stageBoundaryX = 7.5f; // Clamped within arena bounds

        private CharacterController characterController;
        private Vector3 verticalVelocity;
        private Vector3 originalScale;
        private bool isCrouching;

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            originalScale = transform.localScale;
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            // Crucial for Netcode: Disable CharacterController on remote clients and referee server
            // so PhysX doesn't fight NetworkTransform interpolation!
            if (!IsOwner && characterController != null)
            {
                characterController.enabled = false;
            }
        }

        private void Update()
        {
            // CRITICAL NETCODE RULE: Only the owner client evaluates local input!
            if (!IsOwner) return;

            HandleInput();
        }

        private void HandleInput()
        {
            float horizontal = 0f;
            bool jumpPressed = false;
            bool crouchPressed = false;

            // Safe reading via modern Input System package
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) horizontal -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) horizontal += 1f;
                if (kb.wKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame) jumpPressed = true;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) crouchPressed = true;
            }

            // Crouch logic (Visual squashing for 2.5D fighting feel)
            if (crouchPressed && !isCrouching && characterController.isGrounded)
            {
                isCrouching = true;
                transform.localScale = new Vector3(originalScale.x, originalScale.y * 0.6f, originalScale.z);
            }
            else if (!crouchPressed && isCrouching)
            {
                isCrouching = false;
                transform.localScale = originalScale;
            }

            // Horizontal stepping (disabled while crouching, classic fighting game mechanic)
            Vector3 move = Vector3.zero;
            if (!isCrouching)
            {
                move = new Vector3(horizontal * moveSpeed, 0, 0);
            }

            // Gravity & Jumping
            if (characterController.isGrounded)
            {
                verticalVelocity.y = -1f; // Ground stickiness
                if (jumpPressed && !isCrouching)
                {
                    verticalVelocity.y = jumpForce;
                }
            }
            else
            {
                verticalVelocity.y += gravity * Time.deltaTime;
            }

            // Apply movement through CharacterController
            Vector3 finalMovement = (move + verticalVelocity) * Time.deltaTime;
            characterController.Move(finalMovement);

            // Keep Z locked at 0 (Strict 2.5D plane) and clamp within Arena ring boundary
            Vector3 clampedPos = transform.position;
            clampedPos.z = 0f;
            clampedPos.x = Mathf.Clamp(clampedPos.x, -stageBoundaryX, stageBoundaryX);
            transform.position = clampedPos;
        }
    }
}
