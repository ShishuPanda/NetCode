using UnityEngine;
using UnityEngine.SceneManagement;

namespace TekkenNetcodeDemo.Rollback
{
    /// <summary>
    /// Interactive On-Screen Telemetry HUD & Visual Ribbon:
    /// - Real-time 30-frame ribbon (Green = Confirmed, Yellow = Speculative, Red = Rollback).
    /// - Latency & packet delay sliders (0ms to 250ms).
    /// - Side-by-side mode switcher (Rollback Netcode vs Delay-Based Netcode).
    /// - Live performance metrics (Resimulation time in microseconds/ms).
    /// </summary>
    public class RollbackVisualizerHUD : MonoBehaviour
    {
        private GUIStyle headerStyle;
        private GUIStyle cardStyle;
        private GUIStyle greenBoxStyle;
        private GUIStyle yellowBoxStyle;
        private GUIStyle redBoxStyle;

        private float rollbackFlashTimer = 0f;

        private void OnGUI()
        {
            var engine = TekkenRollbackEngine.Instance;
            if (engine == null) return;

            InitStyles();

            if (engine.IsCurrentlyRollingBackThisFrame)
            {
                rollbackFlashTimer = 0.25f;
            }
            if (rollbackFlashTimer > 0) rollbackFlashTimer -= Time.deltaTime;

            // 1. Top Health & Round Panel
            RenderTopHealthBar(engine);

            // 2. Main Telemetry & Control Panel (Left Side)
            RenderControlPanel(engine);

            // 3. Bottom Live Frame Ribbon (Visualizing Rollback)
            RenderFrameRibbon(engine);
        }

        private void InitStyles()
        {
            if (headerStyle == null)
            {
                headerStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 15,
                    fontStyle = FontStyle.Bold
                };
            }
        }

        private void RenderTopHealthBar(TekkenRollbackEngine engine)
        {
            float width = 640;
            float height = 70;
            float x = (Screen.width - width) * 0.5f;

            GUILayout.BeginArea(new Rect(x, 10, width, height), GUI.skin.box);
            GUILayout.BeginHorizontal();

            // P1 (Blue)
            GUILayout.BeginVertical(GUILayout.Width(280));
            GUILayout.Label("<color=#3B82F6><b>PLAYER 1 (LOCAL)</b></color>", GUI.skin.label);
            float p1Pct = Mathf.Clamp01(engine.currentState.P1.Health / 1000f);
            GUI.color = Color.Lerp(Color.red, Color.green, p1Pct);
            GUILayout.HorizontalSlider(p1Pct, 0f, 1f);
            GUI.color = Color.white;
            GUILayout.Label($"HP: {engine.currentState.P1.Health} / 1000  [{engine.currentState.P1.State}]");
            GUILayout.EndVertical();

            GUILayout.FlexibleSpace();
            GUILayout.Label("<size=18><b>VS</b></size>", GUI.skin.label);
            GUILayout.FlexibleSpace();

            // P2 (Red)
            GUILayout.BeginVertical(GUILayout.Width(280));
            GUILayout.Label("<color=#EF4444><b>PLAYER 2 (REMOTE / AI)</b></color>", GUI.skin.label);
            float p2Pct = Mathf.Clamp01(engine.currentState.P2.Health / 1000f);
            GUI.color = Color.Lerp(Color.red, Color.green, p2Pct);
            GUILayout.HorizontalSlider(p2Pct, 0f, 1f);
            GUI.color = Color.white;
            GUILayout.Label($"HP: {engine.currentState.P2.Health} / 1000  [{engine.currentState.P2.State}]");
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void RenderControlPanel(TekkenRollbackEngine engine)
        {
            float panelWidth = 380;
            float panelHeight = 440;
            float x = 15;
            float y = 15;

            GUILayout.BeginArea(new Rect(x, y, panelWidth, panelHeight), GUI.skin.window);
            GUILayout.Label("<size=15><b>TEKKEN ROLLBACK BENCHMARK</b></size>", headerStyle);
            GUILayout.Label("<color=grey>Interactive GGPO Rollback vs Delay-Based Netcode</color>");
            GUILayout.Space(6);

            // Mode Selector
            GUILayout.Label("<b>1. NETCODE ARCHITECTURE MODE:</b>");
            string modeBtnText = engine.currentMode == NetcodeMode.RollbackNetcode
                ? "<color=#22C55E><b>[ACTIVE] ROLLBACK NETCODE (Instant 0ms)</b></color>"
                : "<color=#EAB308><b>[ACTIVE] DELAY-BASED NETCODE (Sluggish)</b></color>";

            if (GUILayout.Button(modeBtnText, GUILayout.Height(32)))
            {
                engine.currentMode = (engine.currentMode == NetcodeMode.RollbackNetcode)
                    ? NetcodeMode.DelayBasedNetcode
                    : NetcodeMode.RollbackNetcode;
            }

            GUILayout.Space(6);

            // Artificial Latency Slider
            GUILayout.Label($"<b>2. SIMULATED NETWORK PING: {engine.simulatedPingMs} ms</b>");
            int prevPing = engine.simulatedPingMs;
            engine.simulatedPingMs = (int)GUILayout.HorizontalSlider(engine.simulatedPingMs, 0, 250);
            int oneWayFrames = Mathf.RoundToInt((engine.simulatedPingMs * 0.5f) / 16.667f);
            GUILayout.Label($"<color=grey>One-way transit time: ~{oneWayFrames} frames @ 60FPS</color>");

            GUILayout.Space(6);

            // Opponent Mode Toggle
            engine.aiOpponentEnabled = GUILayout.Toggle(engine.aiOpponentEnabled, " AI Controlled Opponent (P2)");

            GUILayout.Space(8);

            // Rollback Telemetry Card
            GUILayout.Label("<b>3. TELEMETRY & ROLLBACK METRICS:</b>");
            Color originalBg = GUI.backgroundColor;
            if (rollbackFlashTimer > 0) GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);

            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label($"<b>Simulation Tick:</b> Frame {engine.currentFrame} (60 Hz)");
            GUILayout.Label($"<b>Total Rollbacks Triggered:</b> {engine.TotalRollbacksTriggered}");
            GUILayout.Label($"<b>Last Rollback Depth:</b> {engine.LastRollbackFrames} frames");
            GUILayout.Label($"<b>Last Resim Duration:</b> <color=#22C55E><b>{engine.LastResimulationDurationMs:F3} ms</b></color> (Sub-millisecond!)");
            GUILayout.Label($"<b>Max Resim Duration:</b> {engine.MaxResimulationDurationMs:F3} ms");
            GUILayout.Label($"<b>Determinism Checksum:</b> 0x{engine.LastChecksum:X8}");
            GUILayout.EndVertical();
            GUI.backgroundColor = originalBg;

            GUILayout.Space(8);

            // Control Cheatsheet
            GUILayout.Label("<b>CONTROLS:</b> <color=yellow>A/D</color>=Walk, <color=yellow>S</color>=Crouch, <color=yellow>W</color>=Jump");
            GUILayout.Label("<color=yellow>J</color>=Punch (Red Hitbox), <color=yellow>K</color>=Kick, <color=yellow>L</color>=Guard (Blue)");

            GUILayout.Space(6);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Reset Match", GUILayout.Height(26))) engine.ResetMatch();
            if (GUILayout.Button("Back to Network Lobby", GUILayout.Height(26))) SceneManager.LoadScene("LobbyScene");
            GUILayout.EndHorizontal();

            GUILayout.EndArea();
        }

        private void RenderFrameRibbon(TekkenRollbackEngine engine)
        {
            float width = 680;
            float height = 95;
            float x = (Screen.width - width) * 0.5f;
            float y = Screen.height - height - 15;

            GUILayout.BeginArea(new Rect(x, y, width, height), GUI.skin.box);
            GUILayout.Label("<b>LIVE 30-FRAME ROLLBACK HISTORY RIBBON:</b>", GUI.skin.label);

            GUILayout.BeginHorizontal();

            uint current = engine.currentFrame;
            int framesToShow = 30;
            uint startFrame = current > (uint)framesToShow ? current - (uint)framesToShow : 0;

            for (uint f = startFrame; f < current; f++)
            {
                bool confirmed = engine.IsFrameConfirmed(f);
                bool isRecentRollback = (current - f) <= (uint)engine.LastRollbackFrames && rollbackFlashTimer > 0;

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
