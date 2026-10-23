using System;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// Online co-op menu: Host creates a Relay session and shows a join code,
// Join connects with a code. Builds its own UI at runtime.
public class CoopSession : MonoBehaviour
{
    [SerializeField] int maxPlayers = 4;

    ISession session;
    Font font;
    GameObject menuPanel;
    GameObject inGamePanel;
    InputField codeInput;
    Text statusText;
    Text codeText;
    Button hostButton;
    Button joinButton;

    async void Start()
    {
        BuildUI();
        SetButtons(false);
        SetStatus("Connecting to Unity services...");

        try
        {
            await UnityServices.InitializeAsync();
            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            SetStatus("Ready. Host a game or enter a code to join.");
            SetButtons(true);
        }
        catch (Exception e)
        {
            SetStatus("Could not connect to Unity services: " + e.Message);
            Debug.LogException(e);
        }
    }

    async void Host()
    {
        SetButtons(false);
        SetStatus("Creating session...");
        try
        {
            var options = new SessionOptions { MaxPlayers = maxPlayers }.WithRelayNetwork();
            session = await MultiplayerService.Instance.CreateSessionAsync(options);
            ShowInGame("Join code: " + session.Code);
        }
        catch (Exception e)
        {
            SetStatus("Host failed: " + e.Message);
            SetButtons(true);
            Debug.LogException(e);
        }
    }

    async void Join()
    {
        string code = codeInput.text.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(code))
        {
            SetStatus("Enter a join code first.");
            return;
        }

        SetButtons(false);
        SetStatus("Joining " + code + "...");
        try
        {
            session = await MultiplayerService.Instance.JoinSessionByCodeAsync(code);
            ShowInGame("Joined: " + session.Code);
        }
        catch (Exception e)
        {
            SetStatus("Join failed: " + e.Message);
            SetButtons(true);
            Debug.LogException(e);
        }
    }

    async void Leave()
    {
        if (session != null)
        {
            try { await session.LeaveAsync(); }
            catch (Exception e) { Debug.LogException(e); }
            session = null;
        }
        inGamePanel.SetActive(false);
        menuPanel.SetActive(true);
        SetStatus("Left the session.");
        SetButtons(true);
    }

    void ShowInGame(string label)
    {
        codeText.text = label;
        menuPanel.SetActive(false);
        inGamePanel.SetActive(true);
    }

    void SetStatus(string message) => statusText.text = message;

    void SetButtons(bool interactable)
    {
        hostButton.interactable = interactable;
        joinButton.interactable = interactable;
    }

    // ---------- UI construction ----------

    void BuildUI()
    {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        if (FindAnyObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

        var canvasGO = new GameObject("CoopCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGO.transform.SetParent(transform, false);
        canvasGO.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        // Main menu, centered
        menuPanel = CreatePanel(canvasGO.transform, new Vector2(0.5f, 0.5f), new Vector2(520, 380));
        CreateText(menuPanel.transform, "Online Co-op", 44, FontStyle.Bold, 50);
        hostButton = CreateButton(menuPanel.transform, "Host Game", Host);
        codeInput = CreateInput(menuPanel.transform, "Enter join code");
        joinButton = CreateButton(menuPanel.transform, "Join Game", Join);
        statusText = CreateText(menuPanel.transform, "", 22, FontStyle.Normal, 60);

        // In-game bar, top-left
        inGamePanel = CreatePanel(canvasGO.transform, new Vector2(0f, 1f), new Vector2(380, 170));
        codeText = CreateText(inGamePanel.transform, "", 28, FontStyle.Bold, 40);
        CreateButton(inGamePanel.transform, "Copy Code", () => GUIUtility.systemCopyBuffer = session?.Code ?? "");
        CreateButton(inGamePanel.transform, "Leave", Leave);
        inGamePanel.SetActive(false);
    }

    GameObject CreatePanel(Transform parent, Vector2 anchor, Vector2 size)
    {
        var go = new GameObject("Panel", typeof(Image), typeof(VerticalLayoutGroup));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.anchoredPosition = new Vector2(anchor.x == 0 ? 20 : 0, anchor.y == 1 ? -20 : 0);
        rt.sizeDelta = size;
        go.GetComponent<Image>().color = new Color(0.08f, 0.1f, 0.14f, 0.9f);

        var layout = go.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(24, 24, 20, 20);
        layout.spacing = 12;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        return go;
    }

    Text CreateText(Transform parent, string content, int size, FontStyle style, float height)
    {
        var go = new GameObject("Text", typeof(Text), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        go.GetComponent<LayoutElement>().preferredHeight = height;
        var text = go.GetComponent<Text>();
        text.font = font;
        text.text = content;
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        return text;
    }

    Button CreateButton(Transform parent, string label, Action onClick)
    {
        var go = new GameObject(label, typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        go.GetComponent<LayoutElement>().preferredHeight = 50;
        go.GetComponent<Image>().color = new Color(0.22f, 0.5f, 0.95f);
        var button = go.GetComponent<Button>();
        button.onClick.AddListener(() => onClick());

        var text = CreateText(go.transform, label, 24, FontStyle.Bold, 50);
        Stretch((RectTransform)text.transform);
        return button;
    }

    InputField CreateInput(Transform parent, string placeholder)
    {
        var go = new GameObject("CodeInput", typeof(Image), typeof(InputField), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        go.GetComponent<LayoutElement>().preferredHeight = 50;
        go.GetComponent<Image>().color = Color.white;

        var text = CreateText(go.transform, "", 26, FontStyle.Bold, 50);
        text.color = Color.black;
        text.supportRichText = false;
        Stretch((RectTransform)text.transform);

        var hint = CreateText(go.transform, placeholder, 22, FontStyle.Italic, 50);
        hint.color = new Color(0, 0, 0, 0.4f);
        Stretch((RectTransform)hint.transform);

        var input = go.GetComponent<InputField>();
        input.textComponent = text;
        input.placeholder = hint;
        input.characterValidation = InputField.CharacterValidation.Alphanumeric;
        return input;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }
}
