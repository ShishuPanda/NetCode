using System;
using UnityEngine;

namespace TekkenNetcodeDemo.Rollback
{
    public enum FighterCombatState : byte
    {
        Neutral,
        WalkForward,
        WalkBackward,
        Crouch,
        Jump,
        PunchStartup,
        PunchActive,
        PunchRecovery,
        KickStartup,
        KickActive,
        KickRecovery,
        Guard,
        HitStun,
        BlockStun,
        Knockdown
    }

    [Serializable]
    public struct RollbackFighterState : IEquatable<RollbackFighterState>
    {
        public Vector3 Position;
        public Vector3 Velocity;
        public FighterCombatState State;
        public byte StateFrame;
        public int Health;
        public bool IsGrounded;
        public bool IsFacingRight;
        public bool HasHitConnectedThisAttack;

        public bool Equals(RollbackFighterState other)
        {
            return Position == other.Position &&
                   Velocity == other.Velocity &&
                   State == other.State &&
                   StateFrame == other.StateFrame &&
                   Health == other.Health &&
                   IsGrounded == other.IsGrounded &&
                   IsFacingRight == other.IsFacingRight &&
                   HasHitConnectedThisAttack == other.HasHitConnectedThisAttack;
        }

        public override bool Equals(object obj) => obj is RollbackFighterState other && Equals(other);

        public override int GetHashCode()
        {
            return HashCode.Combine(Position, Velocity, (int)State, StateFrame, Health, IsGrounded, IsFacingRight);
        }
    }

    [Serializable]
    public struct RollbackGameState : IEquatable<RollbackGameState>
    {
        public uint Frame;
        public RollbackFighterState P1;
        public RollbackFighterState P2;
        public bool LastHitWasBlock;
        public byte HitSparkTimer; // Frames to flash hit effect

        public const float MoveSpeed = 4.5f;
        public const float JumpSpeed = 7.0f;
        public const float Gravity = -20.0f;
        public const float ArenaBoundX = 7.5f;
        public const float PunchRange = 1.9f;
        public const float KickRange = 2.4f;

        public static RollbackGameState CreateInitial()
        {
            return new RollbackGameState
            {
                Frame = 0,
                P1 = new RollbackFighterState
                {
                    Position = new Vector3(-3.5f, 0f, 0f),
                    Velocity = Vector3.zero,
                    State = FighterCombatState.Neutral,
                    StateFrame = 0,
                    Health = 1000,
                    IsGrounded = true,
                    IsFacingRight = true
                },
                P2 = new RollbackFighterState
                {
                    Position = new Vector3(3.5f, 0f, 0f),
                    Velocity = Vector3.zero,
                    State = FighterCombatState.Neutral,
                    StateFrame = 0,
                    Health = 1000,
                    IsGrounded = true,
                    IsFacingRight = false
                },
                LastHitWasBlock = false,
                HitSparkTimer = 0
            };
        }

        /// <summary>
        /// Pure deterministic simulation step: advances state by 1 tick (1/60s).
        /// Identical output on all platforms given the same inputs and previous state.
        /// </summary>
        public RollbackGameState Advance(RollbackInput p1Input, RollbackInput p2Input, float dt)
        {
            var next = this;
            next.Frame++;

            if (next.HitSparkTimer > 0) next.HitSparkTimer--;

            // 1. Advance individual fighters
            AdvanceFighter(ref next.P1, p1Input, next.P2.Position.x, dt);
            AdvanceFighter(ref next.P2, p2Input, next.P1.Position.x, dt);

            // 2. Resolve Collisions & Hitboxes
            ResolveCombatInteractions(ref next.P1, ref next.P2, ref next.LastHitWasBlock, ref next.HitSparkTimer);
            ResolveCombatInteractions(ref next.P2, ref next.P1, ref next.LastHitWasBlock, ref next.HitSparkTimer);

            // 3. Body push collision: Fighters cannot walk through each other
            ResolveBodyPush(ref next.P1.Position, ref next.P2.Position);

            return next;
        }

        private static void AdvanceFighter(ref RollbackFighterState f, RollbackInput input, float opponentX, float dt)
        {
            // Face opponent when grounded and not in hitstun/attacking
            if (f.IsGrounded && f.State != FighterCombatState.HitStun && f.State != FighterCombatState.Knockdown)
            {
                f.IsFacingRight = opponentX > f.Position.x;
            }

            // State Machine Transition & Advance
            switch (f.State)
            {
                case FighterCombatState.Neutral:
                case FighterCombatState.WalkForward:
                case FighterCombatState.WalkBackward:
                case FighterCombatState.Crouch:
                    EvaluateNeutralInputs(ref f, input);
                    break;

                case FighterCombatState.Jump:
                    // Airborne physics
                    f.StateFrame++;
                    break;

                case FighterCombatState.PunchStartup:
                    f.StateFrame++;
                    if (f.StateFrame >= 3) { f.State = FighterCombatState.PunchActive; f.StateFrame = 0; }
                    break;
                case FighterCombatState.PunchActive:
                    f.StateFrame++;
                    if (f.StateFrame >= 4) { f.State = FighterCombatState.PunchRecovery; f.StateFrame = 0; }
                    break;
                case FighterCombatState.PunchRecovery:
                    f.StateFrame++;
                    if (f.StateFrame >= 5) { f.State = FighterCombatState.Neutral; f.StateFrame = 0; }
                    break;

                case FighterCombatState.KickStartup:
                    f.StateFrame++;
                    if (f.StateFrame >= 4) { f.State = FighterCombatState.KickActive; f.StateFrame = 0; }
                    break;
                case FighterCombatState.KickActive:
                    f.StateFrame++;
                    if (f.StateFrame >= 5) { f.State = FighterCombatState.KickRecovery; f.StateFrame = 0; }
                    break;
                case FighterCombatState.KickRecovery:
                    f.StateFrame++;
                    if (f.StateFrame >= 6) { f.State = FighterCombatState.Neutral; f.StateFrame = 0; }
                    break;

                case FighterCombatState.Guard:
                    if (!input.IsSet(InputButtons.Guard))
                    {
                        f.State = FighterCombatState.Neutral;
                        f.StateFrame = 0;
                    }
                    else
                    {
                        f.StateFrame++;
                    }
                    break;

                case FighterCombatState.HitStun:
                    f.StateFrame++;
                    if (f.StateFrame >= 12) { f.State = FighterCombatState.Neutral; f.StateFrame = 0; }
                    break;

                case FighterCombatState.BlockStun:
                    f.StateFrame++;
                    if (f.StateFrame >= 6) { f.State = FighterCombatState.Neutral; f.StateFrame = 0; }
                    break;

                case FighterCombatState.Knockdown:
                    f.StateFrame++;
                    break;
            }

            // Apply Gravity
            if (!f.IsGrounded)
            {
                f.Velocity.y += Gravity * dt;
            }

            // Integrate Position
            f.Position += f.Velocity * dt;

            // Ground floor check (y = 0)
            if (f.Position.y <= 0f)
            {
                f.Position.y = 0f;
                f.Velocity.y = 0f;
                f.IsGrounded = true;
                if (f.State == FighterCombatState.Jump)
                {
                    f.State = FighterCombatState.Neutral;
                    f.StateFrame = 0;
                }
            }

            // Clamp within stage ring boundaries
            f.Position.x = Mathf.Clamp(f.Position.x, -ArenaBoundX, ArenaBoundX);
            f.Position.z = 0f; // Strict 2.5D plane
        }

        private static void EvaluateNeutralInputs(ref RollbackFighterState f, RollbackInput input)
        {
            if (input.IsSet(InputButtons.LightPunch))
            {
                f.State = FighterCombatState.PunchStartup;
                f.StateFrame = 0;
                f.Velocity.x = 0;
                f.HasHitConnectedThisAttack = false;
                return;
            }

            if (input.IsSet(InputButtons.HeavyKick))
            {
                f.State = FighterCombatState.KickStartup;
                f.StateFrame = 0;
                f.Velocity.x = 0;
                f.HasHitConnectedThisAttack = false;
                return;
            }

            if (input.IsSet(InputButtons.Guard))
            {
                f.State = FighterCombatState.Guard;
                f.StateFrame = 0;
                f.Velocity.x = 0;
                return;
            }

            if (input.IsSet(InputButtons.Up) && f.IsGrounded)
            {
                f.State = FighterCombatState.Jump;
                f.StateFrame = 0;
                f.Velocity.y = JumpSpeed;
                f.IsGrounded = false;
                return;
            }

            if (input.IsSet(InputButtons.Down))
            {
                f.State = FighterCombatState.Crouch;
                f.StateFrame = 0;
                f.Velocity.x = 0;
                return;
            }

            // Lateral Stepping
            float moveDir = 0f;
            if (input.IsSet(InputButtons.Left)) moveDir -= 1f;
            if (input.IsSet(InputButtons.Right)) moveDir += 1f;

            if (moveDir != 0f)
            {
                bool isAdvancing = (moveDir > 0 && f.IsFacingRight) || (moveDir < 0 && !f.IsFacingRight);
                f.State = isAdvancing ? FighterCombatState.WalkForward : FighterCombatState.WalkBackward;
                f.Velocity.x = moveDir * MoveSpeed;
            }
            else
            {
                f.State = FighterCombatState.Neutral;
                f.Velocity.x = 0f;
            }
        }

        private static void ResolveCombatInteractions(ref RollbackFighterState attacker, ref RollbackFighterState defender, ref bool lastHitBlock, ref byte sparkTimer)
        {
            bool isAttacking = (attacker.State == FighterCombatState.PunchActive) || (attacker.State == FighterCombatState.KickActive);
            if (!isAttacking || attacker.HasHitConnectedThisAttack) return;

            float reach = (attacker.State == FighterCombatState.PunchActive) ? PunchRange : KickRange;
            float dist = Mathf.Abs(attacker.Position.x - defender.Position.x);

            // Check if attacker reaches defender and is facing them
            bool facing = (attacker.IsFacingRight && defender.Position.x >= attacker.Position.x) ||
                          (!attacker.IsFacingRight && defender.Position.x <= attacker.Position.x);

            if (dist <= reach && facing)
            {
                attacker.HasHitConnectedThisAttack = true;
                sparkTimer = 6; // Flash effect for 6 frames

                float pushDir = attacker.IsFacingRight ? 1f : -1f;

                if (defender.State == FighterCombatState.Guard)
                {
                    // Guarded hit (Chip damage & Blockstun)
                    defender.Health = Math.Max(0, defender.Health - 25);
                    defender.State = FighterCombatState.BlockStun;
                    defender.StateFrame = 0;
                    defender.Velocity.x = pushDir * 3.5f;
                    lastHitBlock = true;
                }
                else
                {
                    // Clean hit (Full damage & Hitstun / Knockdown)
                    int dmg = (attacker.State == FighterCombatState.PunchActive) ? 80 : 130;
                    defender.Health = Math.Max(0, defender.Health - dmg);

                    if (defender.Health <= 0)
                    {
                        defender.State = FighterCombatState.Knockdown;
                        defender.Velocity = new Vector3(pushDir * 4.5f, 4.0f, 0f);
                        defender.IsGrounded = false;
                    }
                    else
                    {
                        defender.State = FighterCombatState.HitStun;
                        defender.StateFrame = 0;
                        defender.Velocity.x = pushDir * 5.0f;
                    }
                    lastHitBlock = false;
                }
            }
        }

        private static void ResolveBodyPush(ref Vector3 posA, ref Vector3 posB)
        {
            const float minBodyDist = 1.0f;
            float dist = Mathf.Abs(posA.x - posB.x);
            if (dist < minBodyDist)
            {
                float overlap = (minBodyDist - dist) * 0.5f;
                if (posA.x < posB.x)
                {
                    posA.x -= overlap;
                    posB.x += overlap;
                }
                else
                {
                    posA.x += overlap;
                    posB.x -= overlap;
                }
            }
        }

        public uint CalculateChecksum()
        {
            uint h = Frame;
            h = (h * 397) ^ (uint)(P1.Position.x * 100);
            h = (h * 397) ^ (uint)(P2.Position.x * 100);
            h = (h * 397) ^ (uint)P1.State;
            h = (h * 397) ^ (uint)P2.State;
            h = (h * 397) ^ (uint)P1.Health;
            h = (h * 397) ^ (uint)P2.Health;
            return h;
        }

        public bool Equals(RollbackGameState other)
        {
            return Frame == other.Frame && P1.Equals(other.P1) && P2.Equals(other.P2);
        }

        public override bool Equals(object obj) => obj is RollbackGameState other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Frame, P1, P2);
    }
}
