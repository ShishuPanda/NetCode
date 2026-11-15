using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace TekkenNetcodeDemo
{
    /// <summary>
    /// Demonstrates Server-Authoritative Spawning and Slot Placement:
    /// - Listens for SceneEventType.LoadEventCompleted to ensure ALL clients finish scene loading before spawning.
    /// - Spawns fighters deterministically with ownership assigned to each client.
    /// - Supports up to 4 players.
    /// </summary>
    public class ArenaSpawnManager : MonoBehaviour
    {
        [Header("Spawn Configuration")]
        [SerializeField] private GameObject fighterPrefab;

        // 4 Spawn positions & rotations for arena
        private static readonly Vector3[] SpawnPositions = new Vector3[]
        {
            new Vector3(-4f, 1f, 0f),    // P1: Left side, facing Right
            new Vector3(4f, 1f, 0f),     // P2: Right side, facing Left
            new Vector3(-1.5f, 1f, 2f),  // P3: Back Left, facing Center
            new Vector3(1.5f, 1f, 2f)    // P4: Back Right, facing Center
        };

        private static readonly Quaternion[] SpawnRotations = new Quaternion[]
        {
            Quaternion.Euler(0, 90, 0),
            Quaternion.Euler(0, -90, 0),
            Quaternion.Euler(0, 45, 0),
            Quaternion.Euler(0, -45, 0)
        };

        private bool hasSpawned = false;

        private void Awake()
        {
            var nm = NetworkManager.Singleton;
            if (nm != null)
            {
                // Ensure prefab is registered on clients so deferred spawn triggers complete immediately
                if (fighterPrefab != null && !nm.NetworkConfig.Prefabs.Contains(fighterPrefab))
                {
                    nm.AddNetworkPrefab(fighterPrefab);
                }

                // Listen for network scene events on the server
                if (nm.IsServer && nm.SceneManager != null)
                {
                    nm.SceneManager.OnSceneEvent += HandleSceneEvent;
                }
            }
        }

        private void OnDestroy()
        {
            var nm = NetworkManager.Singleton;
            if (nm != null && nm.SceneManager != null)
            {
                nm.SceneManager.OnSceneEvent -= HandleSceneEvent;
            }
        }

        private void Start()
        {
            // Fallback for direct scene testing (if started directly in ArenaScene without scene loading)
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
            {
                if (NetworkManager.Singleton.SceneManager == null || !NetworkManager.Singleton.IsListening)
                {
                    SpawnFighters();
                }
            }
        }

        /// <summary>
        /// Critical Netcode pattern: Only spawn networked objects AFTER all clients have completed loading the scene!
        /// </summary>
        private void HandleSceneEvent(SceneEvent sceneEvent)
        {
            if (hasSpawned) return;

            if (sceneEvent.SceneEventType == SceneEventType.LoadEventCompleted && sceneEvent.SceneName == "ArenaScene")
            {
                Debug.Log($"[ArenaSpawnManager] All clients confirmed scene load. Spawning fighters now.");
                SpawnFighters();
            }
        }

        private void SpawnFighters()
        {
            if (hasSpawned || NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;
            hasSpawned = true;

            var clientIds = NetworkManager.Singleton.ConnectedClientsIds;
            Debug.Log($"[ArenaSpawnManager] Spawning fighters for {clientIds.Count} connected clients.");

            for (int i = 0; i < clientIds.Count && i < SpawnPositions.Length; i++)
            {
                ulong clientId = clientIds[i];
                Vector3 spawnPos = SpawnPositions[i];
                Quaternion spawnRot = SpawnRotations[i];

                GameObject fighterObj = Instantiate(fighterPrefab, spawnPos, spawnRot);
                var netObj = fighterObj.GetComponent<NetworkObject>();

                // Spawn with ownership transferred to the client
                netObj.SpawnWithOwnership(clientId, destroyWithScene: true);

                // Initialize slot and visual data
                if (fighterObj.TryGetComponent(out NetworkPlayerSlot slot))
                {
                    slot.PlayerIndex.Value = i;
                    slot.DisplayName.Value = $"Player {i + 1}";
                }

                Debug.Log($"[ArenaSpawnManager] Spawned Player {i + 1} for ClientId {clientId} at {spawnPos}");
            }
        }
    }
}
