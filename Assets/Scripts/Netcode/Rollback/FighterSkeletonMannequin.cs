using UnityEngine;

namespace TekkenNetcodeDemo.Rollback
{
    /// <summary>
    /// Procedural Hitbox/Hurtbox Skeleton Mannequin:
    /// - Built entirely with Unity primitives (Zero external asset dependencies).
    /// - Poses mathematically based on FighterCombatState and frame data.
    /// - Dynamically tints limbs: Green = Hurtbox, Red = Active Hitbox, Blue = Guard.
    /// </summary>
    public class FighterSkeletonMannequin : MonoBehaviour
    {
        [Header("Identity")]
        public bool isPlayer1 = true;
        public Color playerThemeColor;

        // Bone Transforms
        private Transform hips;
        private Transform torso;
        private Transform head;
        private Transform lArm, lFist;
        private Transform rArm, rFist;
        private Transform lThigh, lShin, lFoot;
        private Transform rThigh, rShin, rFoot;

        // Renderers for dynamic hitbox coloring
        private Renderer rFistRenderer;
        private Renderer rFootRenderer;
        private Renderer lArmRenderer;
        private Renderer rArmRenderer;

        // Dynamic Materials
        private Material hurtboxMat;
        private Material activeHitboxMat;
        private Material guardMat;
        private Material boneMat;

        public void Initialize(bool p1)
        {
            isPlayer1 = p1;
            playerThemeColor = p1 ? new Color(0.2f, 0.6f, 1f) : new Color(1f, 0.3f, 0.3f);

            CreateMaterials();
            BuildSkeleton();
        }

        private void CreateMaterials()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");

            boneMat = new Material(shader) { color = playerThemeColor };
            hurtboxMat = new Material(shader) { color = new Color(0.2f, 0.9f, 0.4f, 0.8f) }; // Green Hurtbox
            activeHitboxMat = new Material(shader) { color = new Color(1.0f, 0.1f, 0.1f, 1.0f) }; // Red Hitbox
            guardMat = new Material(shader) { color = new Color(0.1f, 0.4f, 1.0f, 0.9f) }; // Blue Guard
        }

        private void BuildSkeleton()
        {
            // Root Hips
            hips = CreateLimb("Hips", PrimitiveType.Sphere, transform, new Vector3(0, 1.0f, 0), new Vector3(0.35f, 0.25f, 0.3f), boneMat);

            // Torso (Spine & Chest Hurtbox)
            torso = CreateLimb("Torso", PrimitiveType.Capsule, hips, new Vector3(0, 0.45f, 0), new Vector3(0.45f, 0.5f, 0.35f), hurtboxMat);

            // Head (High Hurtbox)
            head = CreateLimb("Head", PrimitiveType.Sphere, torso, new Vector3(0, 0.55f, 0), new Vector3(0.32f, 0.35f, 0.32f), hurtboxMat);

            // Left Arm (Guard / Off-hand)
            lArm = CreateLimb("L_Arm", PrimitiveType.Capsule, torso, new Vector3(0.28f, 0.15f, 0.2f), new Vector3(0.16f, 0.35f, 0.16f), hurtboxMat);
            lFist = CreateLimb("L_Fist", PrimitiveType.Sphere, lArm, new Vector3(0, -0.25f, 0.15f), new Vector3(0.22f, 0.22f, 0.22f), hurtboxMat);

            // Right Arm (Lead Strike)
            rArm = CreateLimb("R_Arm", PrimitiveType.Capsule, torso, new Vector3(-0.28f, 0.15f, 0.2f), new Vector3(0.16f, 0.35f, 0.16f), hurtboxMat);
            rFist = CreateLimb("R_Fist", PrimitiveType.Sphere, rArm, new Vector3(0, -0.25f, 0.15f), new Vector3(0.24f, 0.24f, 0.24f), hurtboxMat);

            // Left Leg
            lThigh = CreateLimb("L_Thigh", PrimitiveType.Capsule, hips, new Vector3(0.16f, -0.3f, 0), new Vector3(0.18f, 0.38f, 0.18f), hurtboxMat);
            lShin = CreateLimb("L_Shin", PrimitiveType.Capsule, lThigh, new Vector3(0, -0.4f, 0), new Vector3(0.16f, 0.36f, 0.16f), hurtboxMat);
            lFoot = CreateLimb("L_Foot", PrimitiveType.Cube, lShin, new Vector3(0, -0.22f, 0.1f), new Vector3(0.18f, 0.12f, 0.32f), hurtboxMat);

            // Right Leg (Lead Kick)
            rThigh = CreateLimb("R_Thigh", PrimitiveType.Capsule, hips, new Vector3(-0.16f, -0.3f, 0), new Vector3(0.18f, 0.38f, 0.18f), hurtboxMat);
            rShin = CreateLimb("R_Shin", PrimitiveType.Capsule, rThigh, new Vector3(0, -0.4f, 0), new Vector3(0.16f, 0.36f, 0.16f), hurtboxMat);
            rFoot = CreateLimb("R_Foot", PrimitiveType.Cube, rShin, new Vector3(0, -0.22f, 0.1f), new Vector3(0.18f, 0.12f, 0.32f), hurtboxMat);

            // Cache renderers for dynamic color transitions
            rFistRenderer = rFist.GetComponent<Renderer>();
            rFootRenderer = rFoot.GetComponent<Renderer>();
            lArmRenderer = lArm.GetComponent<Renderer>();
            rArmRenderer = rArm.GetComponent<Renderer>();
        }

        private Transform CreateLimb(string limbName, PrimitiveType type, Transform parent, Vector3 localPos, Vector3 localScale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = limbName;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;

            // Remove native primitive collider so it doesn't interfere with physics simulation
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);

            var rend = go.GetComponent<Renderer>();
            if (rend != null) rend.material = mat;

            return go.transform;
        }

        /// <summary>
        /// Poses the skeleton bones based on the authoritative RollbackFighterState.
        /// </summary>
        public void ApplyState(RollbackFighterState state)
        {
            // Position & Facing Direction
            transform.position = state.Position;
            transform.rotation = Quaternion.Euler(0, state.IsFacingRight ? 90 : -90, 0);

            // Default posture
            hips.localPosition = new Vector3(0, 1.0f, 0);
            hips.localRotation = Quaternion.identity;
            torso.localRotation = Quaternion.identity;
            rArm.localRotation = Quaternion.Euler(30, 20, 0);
            lArm.localRotation = Quaternion.Euler(30, -20, 0);
            rFist.localPosition = new Vector3(0, -0.25f, 0.2f);
            rThigh.localRotation = Quaternion.Euler(10, 0, 0);
            lThigh.localRotation = Quaternion.Euler(-10, 0, 0);

            // Default material states
            rFistRenderer.material = hurtboxMat;
            rFootRenderer.material = hurtboxMat;
            lArmRenderer.material = hurtboxMat;
            rArmRenderer.material = hurtboxMat;

            // Pose based on combat state
            switch (state.State)
            {
                case FighterCombatState.Neutral:
                    // Subtle idle breathing
                    float breath = Mathf.Sin(Time.time * 6f) * 0.03f;
                    torso.localPosition = new Vector3(0, 0.45f + breath, 0);
                    break;

                case FighterCombatState.WalkForward:
                case FighterCombatState.WalkBackward:
                    float walkAngle = Mathf.Sin(Time.time * 12f) * 25f;
                    rThigh.localRotation = Quaternion.Euler(walkAngle, 0, 0);
                    lThigh.localRotation = Quaternion.Euler(-walkAngle, 0, 0);
                    rArm.localRotation = Quaternion.Euler(30 - walkAngle * 0.5f, 20, 0);
                    break;

                case FighterCombatState.Crouch:
                    // Drop hips by 40% & widen stance
                    hips.localPosition = new Vector3(0, 0.65f, 0);
                    torso.localRotation = Quaternion.Euler(15, 0, 0);
                    rThigh.localRotation = Quaternion.Euler(55, 15, 0);
                    lThigh.localRotation = Quaternion.Euler(55, -15, 0);
                    break;

                case FighterCombatState.Jump:
                    hips.localPosition = new Vector3(0, 1.15f, 0);
                    rThigh.localRotation = Quaternion.Euler(45, 0, 0);
                    lThigh.localRotation = Quaternion.Euler(45, 0, 0);
                    break;

                case FighterCombatState.PunchStartup:
                    // Pull right fist back
                    rArm.localRotation = Quaternion.Euler(10, 35, 0);
                    rFist.localPosition = new Vector3(0, -0.15f, -0.1f);
                    break;

                case FighterCombatState.PunchActive:
                    // Extend right fist straight forward along Z (local forward)
                    rArm.localRotation = Quaternion.Euler(85, 0, 0);
                    rFist.localPosition = new Vector3(0, 0.7f, 0.35f);
                    rFistRenderer.material = activeHitboxMat; // TURNS RED!
                    break;

                case FighterCombatState.PunchRecovery:
                    // Returning back
                    rArm.localRotation = Quaternion.Euler(45, 15, 0);
                    rFist.localPosition = new Vector3(0, -0.1f, 0.1f);
                    break;

                case FighterCombatState.KickStartup:
                    rThigh.localRotation = Quaternion.Euler(-20, 0, 0);
                    break;

                case FighterCombatState.KickActive:
                    // High forward side kick
                    rThigh.localRotation = Quaternion.Euler(75, 0, 0);
                    rShin.localRotation = Quaternion.Euler(15, 0, 0);
                    rFootRenderer.material = activeHitboxMat; // TURNS RED!
                    break;

                case FighterCombatState.KickRecovery:
                    rThigh.localRotation = Quaternion.Euler(30, 0, 0);
                    break;

                case FighterCombatState.Guard:
                    // Both forearms cross in front of chest (TURNS BLUE!)
                    rArm.localRotation = Quaternion.Euler(70, -25, 10);
                    lArm.localRotation = Quaternion.Euler(70, 25, -10);
                    rArmRenderer.material = guardMat;
                    lArmRenderer.material = guardMat;
                    rFistRenderer.material = guardMat;
                    break;

                case FighterCombatState.HitStun:
                    // Flinch back
                    torso.localRotation = Quaternion.Euler(-25, 0, 0);
                    head.localRotation = Quaternion.Euler(-20, 0, 0);
                    rArm.localRotation = Quaternion.Euler(-20, 45, 0);
                    lArm.localRotation = Quaternion.Euler(-20, -45, 0);
                    break;

                case FighterCombatState.BlockStun:
                    // Recoil while maintaining guard
                    torso.localRotation = Quaternion.Euler(-10, 0, 0);
                    rArmRenderer.material = guardMat;
                    lArmRenderer.material = guardMat;
                    break;

                case FighterCombatState.Knockdown:
                    // Flat on back
                    hips.localPosition = new Vector3(0, 0.25f, 0);
                    hips.localRotation = Quaternion.Euler(-80, 0, 0);
                    break;
            }
        }
    }
}
