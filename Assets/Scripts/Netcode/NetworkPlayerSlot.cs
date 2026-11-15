using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace TekkenNetcodeDemo
{
    /// <summary>
    /// Demonstrates NetworkVariable synchronization and visual differentiation:
    /// - Player 1 (Index 0): Electric Blue
    /// - Player 2 (Index 1): Crimson Red
    /// - Player 3 (Index 2): Electric Yellow
    /// - Player 4 (Index 3): Emerald Green
    /// </summary>
    public class NetworkPlayerSlot : NetworkBehaviour
    {
        [Header("Network Variables")]
        public NetworkVariable<int> PlayerIndex = new NetworkVariable<int>(
            -1,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        public NetworkVariable<FixedString64Bytes> DisplayName = new NetworkVariable<FixedString64Bytes>(
            "Fighter",
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        [Header("Visuals")]
        [SerializeField] private MeshRenderer bodyRenderer;
        [SerializeField] private TextMesh nameplateText;

        private static readonly Color[] PlayerColors = new Color[]
        {
            new Color(0.23f, 0.51f, 0.96f), // P1: Electric Blue
            new Color(0.94f, 0.27f, 0.27f), // P2: Crimson Red
            new Color(0.92f, 0.70f, 0.03f), // P3: Electric Yellow
            new Color(0.13f, 0.77f, 0.37f)  // P4: Emerald Green
        };

        public override void OnNetworkSpawn()
        {
            PlayerIndex.OnValueChanged += HandlePlayerIndexChanged;
            DisplayName.OnValueChanged += HandleDisplayNameChanged;

            ApplyVisuals(PlayerIndex.Value);
            UpdateNameplate();
        }

        public override void OnNetworkDespawn()
        {
            PlayerIndex.OnValueChanged -= HandlePlayerIndexChanged;
            DisplayName.OnValueChanged -= HandleDisplayNameChanged;
        }

        private void HandlePlayerIndexChanged(int previous, int current)
        {
            ApplyVisuals(current);
            UpdateNameplate();
        }

        private void HandleDisplayNameChanged(FixedString64Bytes previous, FixedString64Bytes current)
        {
            UpdateNameplate();
        }

        public void ApplyVisuals(int index)
        {
            if (bodyRenderer == null)
            {
                bodyRenderer = GetComponentInChildren<MeshRenderer>();
            }

            if (bodyRenderer != null)
            {
                Color targetColor = (index >= 0 && index < PlayerColors.Length) ? PlayerColors[index] : Color.gray;
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                mat.color = targetColor;
                bodyRenderer.material = mat;
            }
        }

        public void UpdateNameplate()
        {
            if (nameplateText == null)
            {
                var textObj = new GameObject("Nameplate");
                textObj.transform.SetParent(transform);
                textObj.transform.localPosition = new Vector3(0, 2.2f, 0);
                textObj.transform.localRotation = Quaternion.Euler(0, 180, 0);
                nameplateText = textObj.AddComponent<TextMesh>();
                nameplateText.characterSize = 0.2f;
                nameplateText.fontSize = 28;
                nameplateText.alignment = TextAlignment.Center;
                nameplateText.anchor = TextAnchor.MiddleCenter;
            }

            int index = PlayerIndex.Value;
            string tag = (index >= 0 && index < 4) ? $"P{index + 1}" : "P?";
            string youTag = IsOwner ? " (YOU)" : "";
            Color tagColor = (index >= 0 && index < PlayerColors.Length) ? PlayerColors[index] : Color.white;

            nameplateText.text = $"[{tag}] {DisplayName.Value}{youTag}";
            nameplateText.color = tagColor;
        }

        private void LateUpdate()
        {
            if (nameplateText != null && Camera.main != null)
            {
                nameplateText.transform.rotation = Camera.main.transform.rotation;
            }
        }
    }
}
