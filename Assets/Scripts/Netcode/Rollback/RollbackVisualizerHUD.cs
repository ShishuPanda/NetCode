using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TekkenNetcodeDemo.Rollback
{
    /// <summary>
    /// Interactive On-Screen Telemetry HUD & Visual Ribbon:
    /// - High-visibility Rollback Alert Banner (flashes during rewinds).
    /// - Arcade Virtual Controller showing active keypresses (A, D, W, S, J, K, L) in real-time.
    /// - Interactive clickable buttons so input works via keyboard OR mouse clicks.
    /// - Fighting game scrolling input history stream.
    /// - Network ping sliders & instant presets (0ms LAN to 250ms severe lag).
    /// - Force Rollback Injection button for immediate demonstration to interviewers.
    /// - Side-by-side mode switcher (Rollback Netcode vs Delay-Based Netcode).
    /// - Live 30-frame history ribbon (Confirmed, Speculative, Rollback).
    /// </summary>
    public class RollbackVisualizerHUD : MonoBehaviour
    {
        private GUIStyle headerStyle;
        private GUIStyle alertStyle;
        private GUIStyle buttonActiveStyle;
        private GUIStyle buttonNormalStyle;
        private GUIStyle inputLogStyle;

        private void OnGUI()
        {
            var engine = TekkenRollbackEngine.Instance;
            if (engine == null) return;

            InitStyles();

            // 1. Top Health & Round Info
            RenderTopHealthBar(engine);

            // 2. High-Impact Rollback Alert Banner
            RenderRollbackAlertBanner(engine);

            // 3. Left Side: Simulation, Netcode Mode & Network Ping Controls
            RenderControlPanel(engine);

            // 4. Right Side: Arcade Virtual Controller & Input History Stream
            RenderArcadeInputOverlay(engine);

            // 5. Bottom: Live 30-Frame Rollback Ribbon
            RenderFrameRibbon(engine);
        }

        private void InitStyles()
        {
            if (headerStyle == null)
            {
                headerStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 14,
                    fontStyle = FontStyle.Bold
                };
            }

            if (alertStyle == null)
            {
                alertStyle = new GUIStyle(GUI.skin.box)
                {
                    fontSize = 13,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
            }

            if (buttonActiveStyle == null)
            {
                buttonActiveStyle = new GUIStyle(GUI.skin.button)
                {
                    fontSize = 12,
                    fontStyle = FontStyle.Bold
                };
            }

            if (buttonNormalStyle == null)
            {
                buttonNormalStyle = new GUIStyle(GUI.skin.button)
                {
                    fontSize = 12
                };
            }

            if (inputLogStyle == null)
            {
                inputLogStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 11,
                    fontStyle = FontStyle.Normal
                };
            }
        }

        private void RenderTopHealthBar(TekkenRollbackEngine engine)
        {
            float width = 640;
            float height = 65;
            float x = (Screen.width - width) * 0.5f;

            GUILayout.BeginArea(new Rect(x, 10, width, height), GUI.skin.box);
            GUILayout.BeginHorizontal();

            // P1 (Blue)
            GUILayout.BeginVertical(GUILayout.Width(270));
            GUILayout.Label("<color=#3B82F6><b>PLAYER 1 (LOCAL)</b></color>", GUI.skin.label);
            float p1Pct = Mathf.Clamp01(engine.currentState.P1.Health / 1000f);
            GUI.color = Color.Lerp(Color.red, Color.green, p1Pct);
            GUILayout.HorizontalSlider(p1Pct, 0f, 1f);
            GUI.color = Color.white;
            GUILayout.Label($"HP: {engine.currentState.P1.Health}/1000  [{engine.currentState.P1.State}]");
            GUILayout.EndVertical();

            GUILayout.FlexibleSpace();
            GUILayout.Label("<size=18><b>VS</b></size>", GUI.skin.label);
            GUILayout.FlexibleSpace();

            // P2 (Red)
            GUILayout.BeginVertical(GUILayout.Width(270));
            GUILayout.Label("<color=#EF4444><b>PLAYER 2 (REMOTE / AI)</b></color>", GUI.skin.label);
            float p2Pct = Mathf.Clamp01(engine.currentState.P2.Health / 1000f);
            GUI.color = Color.Lerp(Color.red, Color.green, p2Pct);
            GUILayout.HorizontalSlider(p2Pct, 0f, 1f);
            GUI.color = Color.white;
            GUILayout.Label($"HP: {engine.currentState.P2.Health}/1000  [{engine.currentState.P2.State}]");
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void RenderRollbackAlertBanner(TekkenRollbackEngine engine)
        {
            float width = 720;
            float height = 40;
            float x = (Screen.width - width) * 0.5f;
            float y = 80;

            Color prevBg = GUI.backgroundColor;

            if (engine.RollbackAlertTimer > 0f)
            {
                // Pulsing red/yellow alert box
                float pulse = Mathf.PingPong(Time.time * 6f, 1f);
                GUI.backgroundColor = Color.Lerp(new Color(1f, 0.2f, 0.2f), new Color(1f, 0.8f, 0.1f), pulse);
                GUI.Box(new Rect(x, y, width, height), $"<size=13><b>{engine.LastRollbackBannerText}</b></size>", alertStyle);
            }
            else
            {
                // Normal green synchronized status
                GUI.backgroundColor = new Color(0.15f, 0.35f, 0.2f);
                string modeStatus = engine.currentMode == NetcodeMode.RollbackNetcode
                    ? "🟢 ROLLBACK ACTIVE: 0ms Local Input Response | Speculative Prediction Sync | Sub-ms Resimulation Ready"
                    : $"🔴 DELAY NETCODE ACTIVE: {engine.simulatedPingMs / 2}ms Input Lag ({Mathf.RoundToInt((engine.simulatedPingMs * 0.5f) / 16.67f)} frames delayed) | Traditional Sluggish Feel";
                GUI.Box(new Rect(x, y, width, height), $"<size=11><b>{modeStatus}</b></size>", alertStyle);
            }

            GUI.backgroundColor = prevBg;
        }

        private void RenderControlPanel(TekkenRollbackEngine engine)
        {
            float panelWidth = 380;
            float panelHeight = 520;
            float x = 15;
            float y = 130;

            GUILayout.BeginArea(new Rect(x, y, panelWidth, panelHeight), GUI.skin.window);
            GUILayout.Label("<size=14><b>NETCODE ARCHITECTURE BENCHMARK</b></size>", headerStyle);
            GUILayout.Label("<color=grey>GGPO Rollback vs Traditional Delay-Based</color>");
            GUILayout.Space(4);

            // 1. Architecture Mode Switcher
            GUILayout.Label("<b>1. NETCODE MODE SWITCH:</b>");
            string modeBtnText = engine.currentMode == NetcodeMode.RollbackNetcode
                ? "<color=#22C55E><b>[ACTIVE] ROLLBACK NETCODE (Instant 0ms)</b></color>"
                : "<color=#EAB308><b>[ACTIVE] DELAY-BASED NETCODE (Sluggish)</b></color>";

            if (GUILayout.Button(modeBtnText, GUILayout.Height(30)))
            {
                engine.currentMode = (engine.currentMode == NetcodeMode.RollbackNetcode)
                    ? NetcodeMode.DelayBasedNetcode
                    : NetcodeMode.RollbackNetcode;
            }

            GUILayout.Space(6);

            // 2. Simulated Latency Slider & Presets
            int oneWayFrames = Mathf.RoundToInt((engine.simulatedPingMs * 0.5f) / 16.667f);
            GUILayout.Label($"<b>2. SIMULATED NETWORK PING: <color=yellow>{engine.simulatedPingMs} ms</color></b>");
            engine.simulatedPingMs = (int)GUILayout.HorizontalSlider(engine.simulatedPingMs, 0, 300);

            // Quick Ping Preset Buttons
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("0ms LAN")) engine.simulatedPingMs = 0;
            if (GUILayout.Button("60ms Broadband")) engine.simulatedPingMs = 60;
            if (GUILayout.Button("130ms Wireless")) engine.simulatedPingMs = 130;
            if (GUILayout.Button("250ms Severe Lag 💥")) engine.simulatedPingMs = 250;
            GUILayout.EndHorizontal();

            GUILayout.Label($"<color=grey>One-way transit time: ~{oneWayFrames} frames | Packets In-Flight: {engine.InFlightPacketsCount}</color>");

            GUILayout.Space(6);

            // 3. Force Rollback Injection Button
            Color prevBg = GUI.backgroundColor;
            GUI.backgroundColor = new Color(1f, 0.35f, 0.35f);
            if (GUILayout.Button("⚡ FORCE ROLLBACK MISPREDICTION NOW", GUILayout.Height(32)))
            {
                engine.TriggerForcedRollbackTest();
            }
            GUI.backgroundColor = prevBg;
            GUILayout.Label("<color=grey><i>Click above to inject an instant desync and witness live rollback!</i></color>");

            GUILayout.Space(6);

            // 4. Opponent Toggle
            engine.aiOpponentEnabled = GUILayout.Toggle(engine.aiOpponentEnabled, " AI Controlled Opponent (P2)");

            GUILayout.Space(6);

            // 5. Telemetry Metrics Box
            GUILayout.Label("<b>3. TELEMETRY & RESIMULATION METRICS:</b>");
            if (engine.RollbackAlertTimer > 0f) GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);

            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label($"<b>Simulation Tick:</b> Frame #{engine.currentFrame:00000} (60 Hz)");
            GUILayout.Label($"<b>Total Rollbacks Triggered:</b> {engine.TotalRollbacksTriggered}");
            GUILayout.Label($"<b>Last Rollback Depth:</b> {engine.LastRollbackFrames} frames");
            GUILayout.Label($"<b>Last Resim Duration:</b> <color=#22C55E><b>{engine.LastResimulationDurationMs:F3} ms</b></color> (Sub-millisecond!)");
            GUILayout.Label($"<b>Max Resim Duration:</b> {engine.MaxResimulationDurationMs:F3} ms");
            GUILayout.Label($"<b>Local Input Latency:</b> {(engine.currentMode == NetcodeMode.RollbackNetcode ? "<color=#22C55E><b>0 ms (Instant Frame 0)</b></color>" : $"<color=#EF4444><b>{engine.simulatedPingMs / 2} ms ({oneWayFrames} frames delay)</b></color>")}");
            GUILayout.Label($"<b>Determinism Checksum:</b> 0x{engine.LastChecksum:X8}");
            GUILayout.EndVertical();
            GUI.backgroundColor = prevBg;

            GUILayout.Space(6);

            // 6. Navigation Buttons
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Reset Match", GUILayout.Height(26))) engine.ResetMatch();
            if (GUILayout.Button("Back to Network Lobby", GUILayout.Height(26))) SceneManager.LoadScene("LobbyScene");
            GUILayout.EndHorizontal();

            GUILayout.EndArea();
        }

        private void RenderArcadeInputOverlay(TekkenRollbackEngine engine)
        {
            float panelWidth = 330;
            float panelHeight = 520;
            float x = Screen.width - panelWidth - 15;
            float y = 130;

            GUILayout.BeginArea(new Rect(x, y, panelWidth, panelHeight), GUI.skin.window);
            GUILayout.Label("<size=14><b>ARCADE INPUT VISUALIZER</b></size>", headerStyle);
            GUILayout.Label("<color=grey>Real-time P1 Key Presses & Hitbox Triggers</color>");
            GUILayout.Space(6);

            RollbackInput p1Input = engine.CurrentP1RawInput;

            // 1. Directional Movement Cluster
            GUILayout.Label("<b>1. DIRECTIONAL MOVEMENT:</b>");
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            RenderKeyButton(engine, "⬆️ W / Jump", p1Input.IsSet(InputButtons.Up), InputButtons.Up, new Color(0.2f, 0.7f, 1f));
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            RenderKeyButton(engine, "⬅️ A / Left", p1Input.IsSet(InputButtons.Left), InputButtons.Left, new Color(1f, 0.8f, 0.2f));
            RenderKeyButton(engine, "⬇️ S / Crouch", p1Input.IsSet(InputButtons.Down), InputButtons.Down, new Color(1f, 0.8f, 0.2f));
            RenderKeyButton(engine, "➡️ D / Right", p1Input.IsSet(InputButtons.Right), InputButtons.Right, new Color(1f, 0.8f, 0.2f));
            GUILayout.EndHorizontal();

            GUILayout.Space(8);

            // 2. Action Attack Cluster
            GUILayout.Label("<b>2. COMBAT ATTACK BUTTONS:</b>");
            GUILayout.BeginHorizontal();
            RenderKeyButton(engine, "🥊 J: PUNCH\n<size=9>🟥 Red Hitbox</size>", p1Input.IsSet(InputButtons.LightPunch), InputButtons.LightPunch, new Color(1f, 0.25f, 0.25f), 42);
            RenderKeyButton(engine, "🦵 K: KICK\n<size=9>🟥 Red Hitbox</size>", p1Input.IsSet(InputButtons.HeavyKick), InputButtons.HeavyKick, new Color(1f, 0.25f, 0.25f), 42);
            RenderKeyButton(engine, "🛡️ L: GUARD\n<size=9>🟦 Blue Shield</size>", p1Input.IsSet(InputButtons.Guard), InputButtons.Guard, new Color(0.2f, 0.6f, 1f), 42);
            GUILayout.EndHorizontal();

            GUILayout.Label("<color=grey><i>Buttons light up when keys are held. You can also CLICK them!</i></color>");

            GUILayout.Space(8);

            // 3. Fighting Game Scrolling Input History
            GUILayout.Label("<b>3. INPUT COMMAND STREAM:</b>");
            GUILayout.BeginVertical(GUI.skin.box);
            if (engine.P1InputHistoryLog.Count == 0)
            {
                GUILayout.Label("<color=grey><i>Press A, D, W, S, J, K, L to see inputs stream here...</i></color>");
            }
            else
            {
                for (int i = 0; i < engine.P1InputHistoryLog.Count; i++)
                {
                    string entry = engine.P1InputHistoryLog[i];
                    string colorHex = (i == 0) ? "#EAB308" : "#94A3B8"; // Highlight latest
                    GUILayout.Label($"<color={colorHex}>{entry}</color>", inputLogStyle);
                }
            }
            GUILayout.EndVertical();

            GUILayout.EndArea();
        }

        private void RenderKeyButton(TekkenRollbackEngine engine, string label, bool isPressed, InputButtons btnFlag, Color activeColor, float height = 34)
        {
            Color prevBg = GUI.backgroundColor;
            if (isPressed)
            {
                GUI.backgroundColor = activeColor;
            }
            else
            {
                GUI.backgroundColor = new Color(0.3f, 0.3f, 0.35f);
            }

            string display = isPressed ? $"<b>{label}\n[PRESSED]</b>" : label;
            if (GUILayout.Button(display, isPressed ? buttonActiveStyle : buttonNormalStyle, GUILayout.Height(height)))
            {
                // Click with mouse also triggers the input!
                engine.QueueVirtualButton(btnFlag);
            }

            GUI.backgroundColor = prevBg;
        }

        private void RenderFrameRibbon(TekkenRollbackEngine engine)
        {
            float width = 720;
            float height = 95;
            float x = (Screen.width - width) * 0.5f;
            float y = Screen.height - height - 12;

            GUILayout.BeginArea(new Rect(x, y, width, height), GUI.skin.box);
            GUILayout.Label("<b>LIVE 30-FRAME ROLLBACK HISTORY RIBBON:</b>", GUI.skin.label);

            GUILayout.BeginHorizontal();

            uint current = engine.currentFrame;
            int framesToShow = 30;
            uint startFrame = current > (uint)framesToShow ? current - (uint)framesToShow : 0;

            for (uint f = startFrame; f < current; f++)
            {
                bool confirmed = engine.IsFrameConfirmed(f);
                bool isRecentRollback = (current - f) <= (uint)engine.LastRollbackFrames && engine.RollbackAlertTimer > 0f;

                string boxText = isRecentRollback ? "R" : confirmed ? "C" : "S";
                Color col = isRecentRollback ? new Color(1f, 0.25f, 0.25f) : confirmed ? new Color(0.2f, 0.8f, 0.4f) : new Color(0.95f, 0.75f, 0.2f);

                Color prevColor = GUI.color;
                GUI.color = col;
                GUILayout.Box(boxText, GUILayout.Width(20), GUILayout.Height(24));
                GUI.color = prevColor;
            }

            GUILayout.EndHorizontal();

            GUILayout.Space(4);
            GUILayout.Label("<color=#22C55E>■ [C] Confirmed</color>  |  <color=#EAB308>■ [S] Speculative Predicted</color>  |  <color=#EF4444>■ [R] Rollback Rewound</color>");
            GUILayout.EndArea();
        }
    }
}
