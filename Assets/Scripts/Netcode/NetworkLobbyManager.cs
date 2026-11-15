using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TekkenNetcodeDemo
{
    /// <summary>
    /// Core Network Lobby Manager demonstrating:
    /// 1. Server Topologies (Host, Dedicated Server, Client).
    /// 2. Server-Authoritative Connection Approval (Supports up to 4 players / spectators).
    /// 3. NetworkVariable synchronization for Player Slots and Ready states.
    /// 4. Synchronized Server Scene Loading (NetworkSceneManager).
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class NetworkLobbyManager : NetworkBehaviour
    {
        public static NetworkLobbyManager Instance { get; private set; }

        public const int MaxPlayers = 4;
        public const string LobbySceneName = "LobbyScene";
        public const string ArenaSceneName = "ArenaScene";
        public const ulong EmptySlot = ulong.MaxValue;

        [Header("Connection Settings")]
        [SerializeField] private string defaultIp = "127.0.0.1";
        [SerializeField] private ushort defaultPort = 7777;
        [SerializeField] private GameObject fighterPrefab;

        // Authoritative ClientId assigned to each slot
        public NetworkVariable<ulong> Slot0ClientId = new NetworkVariable<ulong>(EmptySlot, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public NetworkVariable<ulong> Slot1ClientId = new NetworkVariable<ulong>(EmptySlot, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public NetworkVariable<ulong> Slot2ClientId = new NetworkVariable<ulong>(EmptySlot, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public NetworkVariable<ulong> Slot3ClientId = new NetworkVariable<ulong>(EmptySlot, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        // Authoritative Ready states for each slot
        public NetworkVariable<bool> Slot0Ready = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public NetworkVariable<bool> Slot1Ready = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public NetworkVariable<bool> Slot2Ready = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public NetworkVariable<bool> Slot3Ready = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        // Total connected players synchronized to all clients
        public NetworkVariable<int> ConnectedCount = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        // Synchronized status banner
        public NetworkVariable<FixedString128Bytes> LobbyStatusText = new NetworkVariable<FixedString128Bytes>(
            "Waiting for players to connect...",
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        public event Action OnLobbyStateChanged;

        private void Awake()
        {
            Application.runInBackground = true;

            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            EnsureNetworkConfiguration();
        }

        private void Start()
        {
            EnsureNetworkConfiguration();

            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnected;
            }
        }

        private void EnsureNetworkConfiguration()
        {
            if (NetworkManager.Singleton == null) return;

            var config = NetworkManager.Singleton.NetworkConfig;
            config.ForceSamePrefabs = false;
            config.ConnectionApproval = true;

            // Register fighter prefab if not already present in NetworkManager
            if (fighterPrefab != null && !config.Prefabs.Contains(fighterPrefab))
            {
                NetworkManager.Singleton.AddNetworkPrefab(fighterPrefab);
            }
        }

        public override void OnDestroy()
        {
            if (NetworkManager.Singleton != null)
            {
                if (NetworkManager.Singleton.ConnectionApprovalCallback == ApprovalCheck)
                {
                    NetworkManager.Singleton.ConnectionApprovalCallback = null;
                }
                NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnected;
            }

            if (Instance == this)
            {
                Instance = null;
            }

            base.OnDestroy();
        }

        public override void OnNetworkSpawn()
        {
            Slot0Ready.OnValueChanged += (prev, cur) => OnLobbyStateChanged?.Invoke();
            Slot1Ready.OnValueChanged += (prev, cur) => OnLobbyStateChanged?.Invoke();
            Slot2Ready.OnValueChanged += (prev, cur) => OnLobbyStateChanged?.Invoke();
            Slot3Ready.OnValueChanged += (prev, cur) => OnLobbyStateChanged?.Invoke();
            Slot0ClientId.OnValueChanged += (prev, cur) => OnLobbyStateChanged?.Invoke();
            Slot1ClientId.OnValueChanged += (prev, cur) => OnLobbyStateChanged?.Invoke();
            Slot2ClientId.OnValueChanged += (prev, cur) => OnLobbyStateChanged?.Invoke();
            Slot3ClientId.OnValueChanged += (prev, cur) => OnLobbyStateChanged?.Invoke();
            ConnectedCount.OnValueChanged += (prev, cur) => OnLobbyStateChanged?.Invoke();
            LobbyStatusText.OnValueChanged += (prev, cur) => OnLobbyStateChanged?.Invoke();

            if (IsServer)
            {
                RebuildSlots();
            }
        }

        #region Topology Controls

        public bool StartHostMode()
        {
            EnsureNetworkConfiguration();
            ConfigureTransport(defaultIp, defaultPort);
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.ConnectionApprovalCallback = ApprovalCheck;
            }
            return NetworkManager.Singleton.StartHost();
        }

        public bool StartDedicatedServerMode()
        {
            EnsureNetworkConfiguration();
            ConfigureTransport(defaultIp, defaultPort);
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.ConnectionApprovalCallback = ApprovalCheck;
            }
            return NetworkManager.Singleton.StartServer();
        }

        public bool StartClientMode(string ip, ushort port)
        {
            EnsureNetworkConfiguration();
            ConfigureTransport(string.IsNullOrEmpty(ip) ? defaultIp : ip, port > 0 ? port : defaultPort);
            return NetworkManager.Singleton.StartClient();
        }

        public void Disconnect()
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                NetworkManager.Singleton.Shutdown();
            }

            if (SceneManager.GetActiveScene().name != LobbySceneName)
            {
                SceneManager.LoadScene(LobbySceneName);
            }
        }

        private void ConfigureTransport(string ip, ushort port)
        {
            if (NetworkManager.Singleton == null) return;
            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            if (transport != null)
            {
                transport.SetConnectionData(ip, port);
            }
        }

        #endregion

        #region Server-Authoritative Slot & Connection Management

        private void ApprovalCheck(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            int currentCount = NetworkManager.Singleton.ConnectedClientsIds.Count;

            if (currentCount >= MaxPlayers)
            {
                response.Approved = false;
                response.Reason = $"Lobby Full! Max {MaxPlayers} players allowed.";
                response.Pending = false;
                Debug.LogWarning($"[ConnectionApproval] Rejected Client {request.ClientNetworkId}: Lobby full.");
                return;
            }

            response.Approved = true;
            response.CreatePlayerObject = false; // Spawning deferred to ArenaScene
            response.Pending = false;
            Debug.Log($"[ConnectionApproval] Approved Client {request.ClientNetworkId}. Total: {currentCount + 1}/{MaxPlayers}");
        }

        private void HandleClientConnected(ulong clientId)
        {
            if (!IsServer || !IsSpawned) return;
            RebuildSlots();
        }

        private void HandleClientDisconnected(ulong clientId)
        {
            if (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsServer)
            {
                string reason = NetworkManager.Singleton.DisconnectReason;
                LobbyUI.LastConnectionError = string.IsNullOrEmpty(reason) ? "Disconnected from server." : reason;
                Debug.LogWarning($"[NetworkLobbyManager] Client disconnected. Reason: {LobbyUI.LastConnectionError}");
            }

            if (!IsServer) return;

            if (IsSpawned)
            {
                RebuildSlots();
            }

            // Return to Lobby if match was in progress
            if (SceneManager.GetActiveScene().name == ArenaSceneName)
            {
                Debug.LogWarning("[NetworkLobbyManager] Player disconnected during match. Returning to Lobby.");
                if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && NetworkManager.Singleton.SceneManager != null)
                {
                    NetworkManager.Singleton.SceneManager.LoadScene(LobbySceneName, LoadSceneMode.Single);
                }
                else
                {
                    SceneManager.LoadScene(LobbySceneName);
                }
            }
        }

        /// <summary>
        /// Server authoritatively maps connected clients to slots 0..3 and cleans up disconnected slots.
        /// </summary>
        private void RebuildSlots()
        {
            if (!IsServer || !IsSpawned || NetworkManager.Singleton == null) return;

            var clientIds = NetworkManager.Singleton.ConnectedClientsIds;
            ConnectedCount.Value = clientIds.Count;

            // In Dedicated Server mode, ClientId 0 is not a player, but NGO assigns clientIds[i] to remote players.
            Slot0ClientId.Value = clientIds.Count > 0 ? clientIds[0] : EmptySlot;
            Slot1ClientId.Value = clientIds.Count > 1 ? clientIds[1] : EmptySlot;
            Slot2ClientId.Value = clientIds.Count > 2 ? clientIds[2] : EmptySlot;
            Slot3ClientId.Value = clientIds.Count > 3 ? clientIds[3] : EmptySlot;

            if (Slot0ClientId.Value == EmptySlot) Slot0Ready.Value = false;
            if (Slot1ClientId.Value == EmptySlot) Slot1Ready.Value = false;
            if (Slot2ClientId.Value == EmptySlot) Slot2Ready.Value = false;
            if (Slot3ClientId.Value == EmptySlot) Slot3Ready.Value = false;

            UpdateLobbyStatusText();
            OnLobbyStateChanged?.Invoke();
        }

        private void UpdateLobbyStatusText()
        {
            int count = ConnectedCount.Value;
            if (count == 0)
            {
                LobbyStatusText.Value = "Lobby Empty. Waiting for players...";
            }
            else if (count == 1)
            {
                LobbyStatusText.Value = "1 Player Connected. Waiting for opponents...";
            }
            else
            {
                LobbyStatusText.Value = $"{count} Players Connected! Ready up to launch match.";
            }
        }

        public int GetSlotForClientId(ulong clientId)
        {
            if (Slot0ClientId.Value == clientId) return 0;
            if (Slot1ClientId.Value == clientId) return 1;
            if (Slot2ClientId.Value == clientId) return 2;
            if (Slot3ClientId.Value == clientId) return 3;
            return -1;
        }

        public bool IsSlotReady(int slotIndex)
        {
            return slotIndex switch
            {
                0 => Slot0Ready.Value,
                1 => Slot1Ready.Value,
                2 => Slot2Ready.Value,
                3 => Slot3Ready.Value,
                _ => false
            };
        }

        #endregion

        #region Ready State & Scene Transition

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void ToggleReadyServerRpc(RpcParams rpcParams = default)
        {
            ulong senderId = rpcParams.Receive.SenderClientId;
            int slot = GetSlotForClientId(senderId);

            switch (slot)
            {
                case 0: Slot0Ready.Value = !Slot0Ready.Value; break;
                case 1: Slot1Ready.Value = !Slot1Ready.Value; break;
                case 2: Slot2Ready.Value = !Slot2Ready.Value; break;
                case 3: Slot3Ready.Value = !Slot3Ready.Value; break;
            }

            Debug.Log($"[NetworkLobbyManager] Client {senderId} (Slot {slot}) toggled ready.");
            CheckMatchLaunch();
        }

        private void CheckMatchLaunch()
        {
            if (!IsServer || !IsSpawned || NetworkManager.Singleton == null) return;

            int count = ConnectedCount.Value;
            if (count < 2) return; // Need at least 2 players to auto-launch

            // Check if every connected player has readied up
            bool allReady = true;
            if (Slot0ClientId.Value != EmptySlot && !Slot0Ready.Value) allReady = false;
            if (Slot1ClientId.Value != EmptySlot && !Slot1Ready.Value) allReady = false;
            if (Slot2ClientId.Value != EmptySlot && !Slot2Ready.Value) allReady = false;
            if (Slot3ClientId.Value != EmptySlot && !Slot3Ready.Value) allReady = false;

            if (allReady)
            {
                LobbyStatusText.Value = "All Players Ready! Loading ArenaScene...";
                Debug.Log("[NetworkLobbyManager] All players ready. Loading ArenaScene via NetworkSceneManager.");
                NetworkManager.Singleton.SceneManager.LoadScene(ArenaSceneName, LoadSceneMode.Single);
            }
        }

        #endregion
    }
}
