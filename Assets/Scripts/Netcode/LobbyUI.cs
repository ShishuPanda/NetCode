using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TekkenNetcodeDemo
{
    /// <summary>
    /// Interactive Lobby UI demonstrating server topologies and player ready state synchronization.
    /// Cleanly formatted using IMGUI for zero-dependency plug-and-play execution.
    /// Supports up to 4 player slots (or 1 Dedicated Server + up to 4 players).
    /// </summary>
    public class LobbyUI : MonoBehaviour
    {
        private string ipAddress = "127.0.0.1";
        private string portString = "7777";
        public static string LastConnectionError = "";

        private static readonly Color[] SlotColors = new Color[]
        {
            new Color(0.23f, 0.51f, 0.96f), // P1: Blue
            new Color(0.94f, 0.27f, 0.27f), // P2: Red
            new Color(0.92f, 0.70f, 0.03f), // P3: Yellow
            new Color(0.13f, 0.77f, 0.37f)  // P4: Green
        };

        private static readonly string[] SlotHex = new string[]
        {
            "#3B82F6",
            "#EF4444",
            "#EAB308",
            "#22C55E"
        };

        private void OnGUI()
        {
            var nm = NetworkManager.Singleton;
            var lobby = NetworkLobbyManager.Instance;
            if (nm == null || lobby == null) return;

            float panelWidth = 460;
            float panelHeight = 520;
            float x = (Screen.width - panelWidth) * 0.5f;
            float y = (Screen.height - panelHeight) * 0.5f;

            GUILayout.BeginArea(new Rect(x, y, panelWidth, panelHeight), GUI.skin.window);
            GUILayout.Label("<size=18><b>TEKKEN NETCODE BENCHMARK</b></size>", GUI.skin.label);
            GUILayout.Space(6);

            if (!nm.IsClient && !nm.IsServer)
            {
                RenderOfflineMenu(lobby);
            }
            else
            {
                RenderConnectedLobby(nm, lobby);
            }

            GUILayout.EndArea();
        }

        private void RenderOfflineMenu(NetworkLobbyManager lobby)
        {
            GUILayout.Label("<b>SELECT SERVER TOPOLOGY:</b>");
            GUILayout.Space(5);

            if (GUILayout.Button("1. Start Host (Listen Server - Local Host)", GUILayout.Height(36)))
            {
                lobby.StartHostMode();
            }

            if (GUILayout.Button("2. Start Dedicated Server (Headless Referee)", GUILayout.Height(36)))
            {
                lobby.StartDedicatedServerMode();
            }

            GUILayout.Space(10);
            GUILayout.Label("<b>JOIN AS CLIENT:</b>");
            GUILayout.BeginHorizontal();
            GUILayout.Label("IP:", GUILayout.Width(40));
            ipAddress = GUILayout.TextField(ipAddress, GUILayout.Width(240));
            GUILayout.Label("Port:", GUILayout.Width(40));
            portString = GUILayout.TextField(portString, GUILayout.Width(60));
            GUILayout.EndHorizontal();

            if (ushort.TryParse(portString, out ushort port))
            {
                if (GUILayout.Button("3. Join Match as Client", GUILayout.Height(36)))
                {
                    LastConnectionError = "";
                    bool started = lobby.StartClientMode(ipAddress, port);
                    if (!started)
                    {
                        LastConnectionError = "Failed to start client mode (Invalid socket).";
                    }
                }
            }

            if (!string.IsNullOrEmpty(LastConnectionError))
            {
                GUILayout.Space(8);
                GUILayout.Label($"<color=red><b>Error:</b> {LastConnectionError}</color>");
            }

            GUILayout.Space(12);
            GUILayout.Label("<color=grey><i>Tip: Use 'Multiplayer Play Mode' to test multiple clients inside the Editor!</i></color>");
        }

        private void RenderConnectedLobby(NetworkManager nm, NetworkLobbyManager lobby)
        {
            string role = nm.IsHost ? "HOST (Player 1)" : nm.IsServer ? "DEDICATED SERVER (Referee)" : "CLIENT";
            GUILayout.Label($"<b>Status: CONNECTED as [{role}]</b>");

            string statusMsg = lobby.IsSpawned ? lobby.LobbyStatusText.Value.ToString() : "Synchronizing network state...";
            GUILayout.Label($"<b>Message:</b> {statusMsg}");
            GUILayout.Space(8);

            GUILayout.Label($"<b>PLAYER SLOTS ({lobby.ConnectedCount.Value}/4 Connected):</b>");

            // Render all 4 slots
            RenderSlot(lobby, 0, "P1", lobby.Slot0ClientId.Value, lobby.Slot0Ready.Value, nm.LocalClientId);
            RenderSlot(lobby, 1, "P2", lobby.Slot1ClientId.Value, lobby.Slot1Ready.Value, nm.LocalClientId);
            RenderSlot(lobby, 2, "P3", lobby.Slot2ClientId.Value, lobby.Slot2Ready.Value, nm.LocalClientId);
            RenderSlot(lobby, 3, "P4", lobby.Slot3ClientId.Value, lobby.Slot3Ready.Value, nm.LocalClientId);

            GUILayout.Space(12);

            // Client Ready Button (Gated by lobby.IsSpawned)
            if (nm.IsClient)
            {
                if (lobby.IsSpawned)
                {
                    int mySlot = lobby.GetSlotForClientId(nm.LocalClientId);
                    if (mySlot >= 0)
                    {
                        bool isReady = lobby.IsSlotReady(mySlot);
                        string btnText = isReady ? "Cancel Ready" : "CLICK TO READY UP!";
                        if (GUILayout.Button(btnText, GUILayout.Height(38)))
                        {
                            lobby.ToggleReadyServerRpc();
                        }
                    }
                    else
                    {
                        GUILayout.Box("Assigning to a player slot...", GUILayout.Height(38));
                    }
                }
                else
                {
                    GUILayout.Box("Connecting & synchronizing with server...", GUILayout.Height(38));
                }
            }

            // Server / Host Controls
            if (nm.IsServer)
            {
                GUILayout.Space(8);
                if (GUILayout.Button("Host Override: Force Launch Arena Now", GUILayout.Height(30)))
                {
                    if (nm.IsListening && nm.SceneManager != null)
                    {
                        nm.SceneManager.LoadScene(NetworkLobbyManager.ArenaSceneName, LoadSceneMode.Single);
                    }
                }
            }

            GUILayout.Space(8);
            if (GUILayout.Button("Disconnect / Leave Lobby", GUILayout.Height(28)))
            {
                lobby.Disconnect();
            }
        }

        private void RenderSlot(NetworkLobbyManager lobby, int slotIndex, string tag, ulong clientId, bool isReady, ulong localClientId)
        {
            bool isOccupied = clientId != NetworkLobbyManager.EmptySlot;
            string colorHex = SlotHex[slotIndex];
            string youIndicator = (isOccupied && clientId == localClientId) ? " <b>(YOU)</b>" : "";

            string text;
            if (isOccupied)
            {
                string readyText = isReady ? "<color=green>[ READY ]</color>" : "<color=red>[ NOT READY ]</color>";
                text = $"<color={colorHex}><b>[{tag}]</b></color> Client #{clientId}{youIndicator} - {readyText}";
            }
            else
            {
                text = $"<color=grey>[{tag}] Empty Slot</color>";
            }

            GUILayout.Box(text, GUILayout.Height(26));
        }
    }
}
