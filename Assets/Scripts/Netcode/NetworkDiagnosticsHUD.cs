using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace TekkenNetcodeDemo
{
    /// <summary>
    /// Real-time Network Diagnostics HUD:
    /// - Displays connection role (Host / Server / Client).
    /// - Measures live round-trip time (RTT / Ping) in milliseconds.
    /// - Displays network tick rate and player counts.
    /// - Provides an immediate disconnect button.
    /// </summary>
    public class NetworkDiagnosticsHUD : MonoBehaviour
    {
        [SerializeField] private bool showHUD = true;

        private void OnGUI()
        {
            if (!showHUD) return;

            var nm = NetworkManager.Singleton;
            if (nm == null) return;

            GUILayout.BeginArea(new Rect(Screen.width - 260, 10, 250, 190), GUI.skin.box);
            GUILayout.Label("<b>--- NETWORK DIAGNOSTICS ---</b>");

            if (!nm.IsClient && !nm.IsServer)
            {
                GUILayout.Label("<color=grey>Status: OFFLINE</color>");
            }
            else
            {
                string role = nm.IsHost ? "<color=yellow>HOST (Listen Server)</color>" :
                              nm.IsServer ? "<color=orange>DEDICATED SERVER</color>" :
                              "<color=cyan>CLIENT</color>";

                GUILayout.Label($"Role: {role}");
                GUILayout.Label($"Local ClientId: {nm.LocalClientId}");
                GUILayout.Label($"Connected Players: {nm.ConnectedClientsIds.Count}/2");

                // Ping / RTT measurement via Unity Transport
                var transport = nm.GetComponent<UnityTransport>();
                if (transport != null && nm.IsClient)
                {
                    ulong serverId = NetworkManager.ServerClientId;
                    ulong rtt = transport.GetCurrentRtt(serverId);
                    string rttColor = rtt < 50 ? "green" : (rtt < 100 ? "yellow" : "red");
                    GUILayout.Label($"Ping (RTT): <color={rttColor}>{rtt} ms</color>");
                }

                if (nm.NetworkTickSystem != null)
                {
                    GUILayout.Label($"Tick Rate: {nm.NetworkTickSystem.TickRate} Hz");
                }

                GUILayout.Space(5);
                if (GUILayout.Button("Leave / Disconnect"))
                {
                    if (NetworkLobbyManager.Instance != null)
                    {
                        NetworkLobbyManager.Instance.Disconnect();
                    }
                    else
                    {
                        nm.Shutdown();
                    }
                }
            }

            GUILayout.EndArea();
        }
    }
}
