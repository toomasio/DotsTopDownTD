// Assets/Scripts/UI/MainMenuController.cs
using System.Collections;
using DotsTopDownTD.Network;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;

namespace DotsTopDownTD.UI
{
    [RequireComponent(typeof(UIDocument))]
    public class MainMenuController : MonoBehaviour
    {
        private VisualElement root;

        // Buttons
        private Button hostLocalButton;
        private Button hostRelayButton;
        private Button joinRelayButton;
        private Button joinIpButton;
        private Button copyCodeButton;
        private Button quitButton;

        // Fields
        private VisualElement relayCodeRow;
        private TextField allocationCodeField;
        private TextField joinCodeField;
        private TextField ipField;
        private IntegerField portField;

        // Status
        private Label statusLabel;

        private void Awake()
        {
            var doc = GetComponent<UIDocument>();
            root = doc.rootVisualElement;

            // === Cache UI Elements ===
            hostLocalButton = root.Q<Button>("host-local-button");
            hostRelayButton = root.Q<Button>("host-relay-button");
            joinRelayButton = root.Q<Button>("join-relay-button");
            joinIpButton = root.Q<Button>("join-ip-button");
            copyCodeButton = root.Q<Button>("copy-code-button");
            quitButton = root.Q<Button>("quit-button");

            relayCodeRow = root.Q<VisualElement>("relay-code-row");
            allocationCodeField = root.Q<TextField>("allocation-code-field");
            joinCodeField = root.Q<TextField>("join-code-field");
            ipField = root.Q<TextField>("ip-field");
            portField = root.Q<IntegerField>("port-field");
            statusLabel = root.Q<Label>("status-label");

            // === Bind Events ===
            hostLocalButton.clicked += OnHostLocalClicked;
            hostRelayButton.clicked += OnHostRelayClicked;
            joinRelayButton.clicked += OnJoinRelayClicked;
            joinIpButton.clicked += OnJoinIpClicked;
            copyCodeButton.clicked += OnCopyCodeClicked;
            quitButton.clicked += OnQuitClicked;

            // Initial state
            allocationCodeField.isReadOnly = true;
            relayCodeRow.style.display = DisplayStyle.None;
            copyCodeButton.SetEnabled(false);

            UpdateStatus("Ready");
        }

        private void OnEnable()
        {
            GameBootstrap.OnConnectionComplete += OnConnectionEstablished;
        }

        private void OnDisable()
        {
            GameBootstrap.OnConnectionComplete -= OnConnectionEstablished;
        }

        private void OnHostLocalClicked()
        {
            SetButtonsEnabled(false);
            UpdateStatus("Starting LAN host...");
            GameBootstrap.StartHostDirect();
        }

        private void OnHostRelayClicked()
        {
            SetButtonsEnabled(false);
            UpdateStatus("Creating Relay allocation...");

            // Show the row early so user sees something happening
            relayCodeRow.style.display = DisplayStyle.Flex;
            allocationCodeField.value = "Generating...";

            // Start hosting and wait for join code
            GameBootstrap.StartHostRelay(maxConnections: 4);
            StartCoroutine(WaitAndDisplayJoinCode());
        }

        private IEnumerator WaitAndDisplayJoinCode()
        {
            // Wait until code is available
            yield return new WaitUntil(() => !string.IsNullOrEmpty(GameRelayData.LastGeneratedJoinCode));

            allocationCodeField.value = GameRelayData.LastGeneratedJoinCode;
            copyCodeButton.SetEnabled(true);
            UpdateStatus($"Hosting via Relay! Share code: {GameRelayData.LastGeneratedJoinCode}");
        }

        private void OnJoinRelayClicked()
        {
            string code = joinCodeField.value.Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(code) || code.Length != 6)
            {
                UpdateStatus("Enter a valid 6-letter code!");
                return;
            }

            SetButtonsEnabled(false);
            UpdateStatus("Joining via Relay...");
            GameBootstrap.JoinRelay(code);
        }

        private void OnJoinIpClicked()
        {
            string ip = ipField.value.Trim();
            ushort port = (ushort)Mathf.Clamp(portField.value, 1, 65535);

            if (string.IsNullOrEmpty(ip))
            {
                UpdateStatus("Enter a valid IP address!");
                return;
            }

            SetButtonsEnabled(false);
            UpdateStatus($"Connecting to {ip}:{port}...");
            GameBootstrap.JoinDirect(ip, port);
        }

        private void OnCopyCodeClicked()
        {
            GUIUtility.systemCopyBuffer = allocationCodeField.value;
            UpdateStatus("Join code copied to clipboard!");
        }

        private void OnQuitClicked()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void OnConnectionEstablished()
        {
            UpdateStatus("Connected! Loading game...");
            Invoke(nameof(LoadGameScene), 0.8f);
        }

        private void LoadGameScene()
        {
            SceneManager.LoadSceneAsync("Game");
        }

        private void SetButtonsEnabled(bool enabled)
        {
            hostLocalButton?.SetEnabled(enabled);
            hostRelayButton?.SetEnabled(enabled);
            joinRelayButton?.SetEnabled(enabled);
            joinIpButton?.SetEnabled(enabled);
            joinCodeField?.SetEnabled(enabled);
            ipField?.SetEnabled(enabled);
            portField?.SetEnabled(enabled);
        }

        private void UpdateStatus(string msg)
        {
            if (statusLabel != null)
                statusLabel.text = msg;

            Debug.Log($"[MainMenu] {msg}");
        }
    }

    // Static helper — make sure this exists (used by ConnectionManager)
    public static class GameRelayData
    {
        public static string LastGeneratedJoinCode { get; set; } = "";
    }
}