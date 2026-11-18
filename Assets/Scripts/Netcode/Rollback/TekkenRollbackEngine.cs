using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TekkenNetcodeDemo.Rollback
{
    public enum NetcodeMode
    {
        RollbackNetcode, // Instant Frame 0 local response + Speculative Prediction & Resimulation
        DelayBasedNetcode // Local inputs delayed by network latency (traditional sluggish feel)
    }

    /// <summary>
    /// Interactive Rollback Netcode Engine & Benchmarking Core:
    /// - Operates at a strict, deterministic 60Hz tick rate.
    /// - Circular state ring buffer storing the last 128 frames.
    /// - Real-time artificial network latency & jitter simulation.
    /// - Microsecond-precision rollback rewind & fast-forward resimulation.
    /// - Side-by-side mode comparison (Rollback vs Delay-Based).
    /// </summary>
    public class TekkenRollbackEngine : MonoBehaviour
    {
        public static TekkenRollbackEngine Instance { get; private set; }

        public const int TargetFPS = 60;
        public const float FixedDeltaTime = 1f / TargetFPS;
        public const int HistoryCapacity = 128;

        [Header("Netcode Configuration")]
        public NetcodeMode currentMode = NetcodeMode.RollbackNetcode;
        [Range(0, 300)] public int simulatedPingMs = 80; // 80ms ping = ~40ms one-way (2-3 frames delay)
        public bool aiOpponentEnabled = true;

        [Header("State History Ring Buffers")]
        private RollbackGameState[] stateRingBuffer = new RollbackGameState[HistoryCapacity];
        private RollbackInput[] p1InputHistory = new RollbackInput[HistoryCapacity];
        private RollbackInput[] p2InputHistory = new RollbackInput[HistoryCapacity];
        private bool[] p2InputConfirmed = new bool[HistoryCapacity];

        // Active State
        public uint currentFrame = 0;
        public RollbackGameState currentState;

        // Simulated Network In-Flight Packet Queue
        private struct DelayedPacket
        {
            public uint TargetArrivalFrame;
            public RollbackInput Input;
        }
        private Queue<DelayedPacket> p2NetworkQueue = new Queue<DelayedPacket>();
        private Queue<RollbackInput> delayModeP1Buffer = new Queue<RollbackInput>();

        // Mannequin Skeletons
        private FighterSkeletonMannequin p1Mannequin;
        private FighterSkeletonMannequin p2Mannequin;

        // Telemetry & Metrics
        public int TotalRollbacksTriggered { get; private set; }
        public int LastRollbackFrames { get; private set; }
        public double LastResimulationDurationMs { get; private set; }
        public double MaxResimulationDurationMs { get; private set; }
        public uint LastChecksum { get; private set; }
        public bool IsCurrentlyRollingBackThisFrame { get; private set; }

        // Timing accumulator for 60Hz tick
        private float tickAccumulator = 0f;
        private Stopwatch resimStopwatch = new Stopwatch();

        private void Awake()
        {
            Instance = this;
            InitializeSimulation();
            SpawnMannequins();
        }

        private void InitializeSimulation()
        {
            currentFrame = 0;
            currentState = RollbackGameState.CreateInitial();
            stateRingBuffer[0] = currentState;

            for (int i = 0; i < HistoryCapacity; i++)
            {
                p1InputHistory[i] = new RollbackInput(0, InputButtons.None);
                p2InputHistory[i] = new RollbackInput(0, InputButtons.None);
                p2InputConfirmed[i] = false;
            }

            p2NetworkQueue.Clear();
            delayModeP1Buffer.Clear();

            TotalRollbacksTriggered = 0;
            LastRollbackFrames = 0;
            LastResimulationDurationMs = 0;
            MaxResimulationDurationMs = 0;
        }

        private void SpawnMannequins()
        {
            var p1Go = new GameObject("P1_Fighter_Skeleton");
            p1Mannequin = p1Go.AddComponent<FighterSkeletonMannequin>();
            p1Mannequin.Initialize(true);

            var p2Go = new GameObject("P2_Fighter_Skeleton");
            p2Mannequin = p2Go.AddComponent<FighterSkeletonMannequin>();
            p2Mannequin.Initialize(false);

            p1Mannequin.ApplyState(currentState.P1);
            p2Mannequin.ApplyState(currentState.P2);
        }

        private void Update()
        {
            // Accumulate delta time for fixed 60Hz deterministic tick
            tickAccumulator += Time.deltaTime;

            while (tickAccumulator >= FixedDeltaTime)
            {
                StepSimulation();
                tickAccumulator -= FixedDeltaTime;
            }

            // Visual updates
            p1Mannequin.ApplyState(currentState.P1);
            p2Mannequin.ApplyState(currentState.P2);
        }

        /// <summary>
        /// Executes a single 60Hz deterministic simulation step.
        /// </summary>
        private void StepSimulation()
        {
            IsCurrentlyRollingBackThisFrame = false;
            uint frame = currentFrame;
            int bufferIndex = (int)(frame % HistoryCapacity);

            // 1. Gather raw inputs
            RollbackInput rawP1Input = SamplePlayer1Input(frame);
            RollbackInput rawP2Input = aiOpponentEnabled ? SampleAiOpponentInput(frame) : SamplePlayer2KeyboardInput(frame);

            // 2. Queue simulated remote network packet
            int oneWayDelayFrames = Mathf.Max(0, Mathf.RoundToInt((simulatedPingMs * 0.5f) / (FixedDeltaTime * 1000f)));
            p2NetworkQueue.Enqueue(new DelayedPacket
            {
                TargetArrivalFrame = frame + (uint)oneWayDelayFrames,
                Input = rawP2Input
            });

            // 3. Process incoming network packets and detect Rollback
            ProcessIncomingPackets();

            // 4. Resolve Local P1 Input based on Mode
            RollbackInput effectiveP1Input;
            if (currentMode == NetcodeMode.DelayBasedNetcode && oneWayDelayFrames > 0)
            {
                // In delay-based netcode, local input is queued and delayed!
                delayModeP1Buffer.Enqueue(rawP1Input);
                if (delayModeP1Buffer.Count > oneWayDelayFrames)
                {
                    effectiveP1Input = delayModeP1Buffer.Dequeue();
                }
                else
                {
                    effectiveP1Input = new RollbackInput(frame, InputButtons.None); // Stuck waiting for delay!
                }
            }
            else
            {
                // ROLLBACK NETCODE: 0ms local execution! Instant response on frame 0!
                effectiveP1Input = rawP1Input;
            }

            // 5. Resolve P2 Input (Speculative prediction if not yet arrived)
            RollbackInput effectiveP2Input;
            if (p2InputConfirmed[bufferIndex])
            {
                effectiveP2Input = p2InputHistory[bufferIndex];
            }
            else
            {
                // Speculative prediction: repeat previous input
                int prevIndex = (int)((frame > 0 ? frame - 1 : 0) % HistoryCapacity);
                effectiveP2Input = new RollbackInput(frame, p2InputHistory[prevIndex].Buttons);
                p2InputHistory[bufferIndex] = effectiveP2Input;
                p2InputConfirmed[bufferIndex] = false;
            }

            p1InputHistory[bufferIndex] = effectiveP1Input;

            // 6. Advance simulation forward by 1 tick
            currentState = currentState.Advance(effectiveP1Input, effectiveP2Input, FixedDeltaTime);

            // Save snapshot in circular ring buffer
            int nextBufferIndex = (int)((frame + 1) % HistoryCapacity);
            stateRingBuffer[nextBufferIndex] = currentState;

            LastChecksum = currentState.CalculateChecksum();
            currentFrame++;
        }

        /// <summary>
        /// Reads delayed network packets. If received packet differs from speculative prediction,
        /// performs an immediate state rollback and fast-forward resimulation!
        /// </summary>
        private void ProcessIncomingPackets()
        {
            while (p2NetworkQueue.Count > 0 && p2NetworkQueue.Peek().TargetArrivalFrame <= currentFrame)
            {
                var packet = p2NetworkQueue.Dequeue();
                uint packetFrame = packet.Input.Frame;

                if (packetFrame >= currentFrame) continue;

                int packetIndex = (int)(packetFrame % HistoryCapacity);
                RollbackInput predictedInput = p2InputHistory[packetIndex];
                RollbackInput confirmedInput = packet.Input;

                p2InputHistory[packetIndex] = confirmedInput;
                p2InputConfirmed[packetIndex] = true;

                // Check if the confirmed input differs from our speculative prediction
                if (currentMode == NetcodeMode.RollbackNetcode && predictedInput != confirmedInput)
                {
                    ExecuteRollback(packetFrame);
                }
            }
        }

        /// <summary>
        /// THE CORE ROLLBACK ALGORITHM:
        /// 1. Rewind state to packetFrame.
        /// 2. Re-simulate from packetFrame to currentFrame in a tight loop.
        /// </summary>
        private void ExecuteRollback(uint rollbackToFrame)
        {
            resimStopwatch.Restart();
            IsCurrentlyRollingBackThisFrame = true;
            TotalRollbacksTriggered++;
            LastRollbackFrames = (int)(currentFrame - rollbackToFrame);

            // 1. Rewind state to the confirmed snapshot
            int rewindIndex = (int)(rollbackToFrame % HistoryCapacity);
            RollbackGameState simState = stateRingBuffer[rewindIndex];

            // 2. Fast-forward re-simulation loop (Executed in < 0.5 milliseconds!)
            for (uint f = rollbackToFrame; f < currentFrame; f++)
            {
                int idx = (int)(f % HistoryCapacity);
                RollbackInput in1 = p1InputHistory[idx];
                RollbackInput in2 = p2InputHistory[idx]; // Now using confirmed input!

                simState = simState.Advance(in1, in2, FixedDeltaTime);

                int nextIdx = (int)((f + 1) % HistoryCapacity);
                stateRingBuffer[nextIdx] = simState;
            }

            // 3. Apply corrected state back to present
            currentState = simState;

            resimStopwatch.Stop();
            LastResimulationDurationMs = resimStopwatch.Elapsed.TotalMilliseconds;
            if (LastResimulationDurationMs > MaxResimulationDurationMs)
            {
                MaxResimulationDurationMs = LastResimulationDurationMs;
            }
        }

        #region Input Sampling

        private RollbackInput SamplePlayer1Input(uint frame)
        {
            InputButtons buttons = InputButtons.None;
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) buttons |= InputButtons.Left;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) buttons |= InputButtons.Right;
                if (kb.wKey.isPressed || kb.spaceKey.isPressed || kb.upArrowKey.isPressed) buttons |= InputButtons.Up;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) buttons |= InputButtons.Down;
                if (kb.jKey.isPressed) buttons |= InputButtons.LightPunch;
                if (kb.kKey.isPressed) buttons |= InputButtons.HeavyKick;
                if (kb.lKey.isPressed) buttons |= InputButtons.Guard;
            }
            return new RollbackInput(frame, buttons);
        }

        private RollbackInput SamplePlayer2KeyboardInput(uint frame)
        {
            InputButtons buttons = InputButtons.None;
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.numpad4Key.isPressed) buttons |= InputButtons.Left;
                if (kb.numpad6Key.isPressed) buttons |= InputButtons.Right;
                if (kb.numpad8Key.isPressed) buttons |= InputButtons.Up;
                if (kb.numpad5Key.isPressed || kb.numpad2Key.isPressed) buttons |= InputButtons.Down;
                if (kb.numpad1Key.isPressed) buttons |= InputButtons.LightPunch;
                if (kb.numpad3Key.isPressed) buttons |= InputButtons.HeavyKick;
                if (kb.numpad0Key.isPressed) buttons |= InputButtons.Guard;
            }
            return new RollbackInput(frame, buttons);
        }

        private float aiNextDecisionTime = 0f;
        private InputButtons aiCurrentButtons = InputButtons.None;

        private RollbackInput SampleAiOpponentInput(uint frame)
        {
            if (Time.time >= aiNextDecisionTime)
            {
                aiNextDecisionTime = Time.time + UnityEngine.Random.Range(0.25f, 0.7f);

                float r = UnityEngine.Random.value;
                if (r < 0.35f)
                {
                    // Advance towards P1
                    aiCurrentButtons = (currentState.P2.Position.x > currentState.P1.Position.x) ? InputButtons.Left : InputButtons.Right;
                }
                else if (r < 0.55f)
                {
                    // Guard stance
                    aiCurrentButtons = InputButtons.Guard;
                }
                else if (r < 0.80f)
                {
                    // Counter-attack
                    aiCurrentButtons = (UnityEngine.Random.value > 0.5f) ? InputButtons.LightPunch : InputButtons.HeavyKick;
                }
                else
                {
                    aiCurrentButtons = InputButtons.None; // Neutral
                }
            }

            return new RollbackInput(frame, aiCurrentButtons);
        }

        #endregion

        public void ResetMatch()
        {
            InitializeSimulation();
        }

        public bool IsFrameConfirmed(uint frame)
        {
            if (frame >= currentFrame) return false;
            int idx = (int)(frame % HistoryCapacity);
            return p2InputConfirmed[idx];
        }
    }
}
