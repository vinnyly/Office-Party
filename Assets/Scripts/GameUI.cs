using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class GameUI : MonoBehaviour
{
    private GameObject canvasObject;
    private GameObject overlay;
    private GameObject hud;

    private TMP_Text title;
    private TMP_Text scoreText;
    private TMP_Text details;
    private TMP_Text status;
    private TMP_Text hudText;
    private TMP_Text playLabel;

    private Button playButton;
    private Button menuButton;

    // Creates the overlay and HUD once, using the existing main camera view.
    public bool Build(GameController controller)
    {
        if (canvasObject != null)
            return true;

        if (TMP_Settings.defaultFontAsset == null)
        {
            Debug.LogError(
                "Import TMP Essential Resources from Window > TextMeshPro.",
                this
            );

            return false;
        }

        EnsureEventSystem();

        canvasObject = new GameObject(
            "Game UI",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster)
        );

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler =
            canvasObject.GetComponent<CanvasScaler>();

        scaler.uiScaleMode =
            CanvasScaler.ScaleMode.ScaleWithScreenSize;

        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform hudRect = CreateRect(
            "HUD",
            canvasObject.transform,
            new Vector2(0, -40),
            new Vector2(1000, 56)
        );

        hudRect.anchorMin = hudRect.anchorMax =
            new Vector2(0.5f, 1f);

        hud = hudRect.gameObject;

        Image hudBackground = hud.AddComponent<Image>();
        hudBackground.color = new Color(0.03f, 0.05f, 0.08f, 0.85f);
        hudBackground.raycastTarget = false;

        hudText = CreateText(
            "HUD Text",
            hudRect,
            Vector2.zero,
            new Vector2(960, 52),
            24
        );

        RectTransform overlayRect = CreateRect(
            "Overlay",
            canvasObject.transform,
            Vector2.zero,
            Vector2.zero
        );

        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = overlayRect.offsetMax = Vector2.zero;

        overlay = overlayRect.gameObject;
        overlay.AddComponent<Image>().color =
            new Color(0, 0, 0, 0.55f);

        RectTransform card = CreateRect(
            "Menu",
            overlayRect,
            Vector2.zero,
            new Vector2(620, 440)
        );

        card.gameObject.AddComponent<Image>().color =
            new Color(0.06f, 0.08f, 0.12f, 0.97f);

        title = CreateText(
            "Title",
            card,
            new Vector2(0, 155),
            new Vector2(570, 60),
            42
        );

        scoreText = CreateText(
            "Score",
            card,
            new Vector2(0, 100),
            new Vector2(570, 40),
            28
        );

        details = CreateText(
            "Details",
            card,
            new Vector2(0, 38),
            new Vector2(570, 78),
            18
        );

        playButton = CreateButton(
            "Play",
            card,
            -44,
            controller.Play,
            out playLabel
        );

        menuButton = CreateButton(
            "Main Menu",
            card,
            -102,
            controller.ShowMenu,
            out _
        );

        status = CreateText(
            "Status",
            card,
            new Vector2(0, -172),
            new Vector2(570, 70),
            16
        );

        status.overflowMode = TextOverflowModes.Ellipsis;

        return true;
    }

    // Displays the saved high score and the button that starts a round.
    public void ShowMenu(int highScore, string instructions)
    {
        ShowOverlay(
            "BOSS SURVIVAL",
            $"High score: {highScore}",
            instructions,
            "Play"
        );

        menuButton.gameObject.SetActive(false);
    }

    // Hides the menu so only the gameplay HUD remains.
    public void ShowPlaying()
    {
        overlay.SetActive(false);
        hud.SetActive(true);

        EventSystem.current.SetSelectedGameObject(null);
    }

    // Displays the round result while leaving the frozen scene visible behind it.
    public void ShowResult(
        bool won,
        int score,
        int highScore,
        double elapsed,
        int seen)
    {
        ShowOverlay(
            won ? "You win!" : "You lose!",
            $"Score: {score}",
            $"High score: {highScore}\n" +
            $"Survived: {elapsed:0.00}s | Seen: {seen}s",
            "Play Again"
        );

        menuButton.gameObject.SetActive(true);
    }

    // Applies the shared layout used by the start menu and result screen.
    private void ShowOverlay(
        string heading,
        string score,
        string description,
        string button)
    {
        overlay.SetActive(true);
        hud.SetActive(false);

        title.text = heading;
        scoreText.text = score;
        details.text = description;
        playLabel.text = button;
        status.text = "";

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        EventSystem.current.SetSelectedGameObject(
            playButton.gameObject
        );
    }

    // Displays a save or setup problem without adding extra menu controls.
    public void SetStatus(string message)
    {
        status.text = message;
    }

    // Displays the timer, whole-number score and current visibility.
    public void UpdateHud(double remaining, int score, bool seen)
    {
        hudText.text =
            $"Time: {remaining:0.0}s     Score: {score}     " +
            (seen ? "SEEN" : "HIDDEN");
    }

    // Creates or reuses the event system and enables new Input System UI controls.
    private static void EnsureEventSystem()
    {
        EventSystem system = EventSystem.current;

        if (system == null)
        {
            system = new GameObject(
                "EventSystem",
                typeof(EventSystem)
            ).GetComponent<EventSystem>();
        }

        StandaloneInputModule oldModule =
            system.GetComponent<StandaloneInputModule>();

        if (oldModule != null)
        {
            oldModule.enabled = false;
        }

        InputSystemUIInputModule module =
            system.GetComponent<InputSystemUIInputModule>();

        if (module == null)
        {
            module =
                system.gameObject.AddComponent<InputSystemUIInputModule>();
        }

        module.enabled = true;
    }

    // Creates a centered UI rectangle with the requested size and position.
    private static RectTransform CreateRect(
        string name,
        Transform parent,
        Vector2 position,
        Vector2 size)
    {
        RectTransform rect = new GameObject(
            name,
            typeof(RectTransform)
        ).GetComponent<RectTransform>();

        rect.SetParent(parent, false);

        rect.anchorMin = rect.anchorMax = rect.pivot =
            new Vector2(0.5f, 0.5f);

        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        return rect;
    }

    // Adds a centered text label that does not block mouse clicks.
    private static TMP_Text CreateText(
        string name,
        Transform parent,
        Vector2 position,
        Vector2 size,
        float fontSize)
    {
        TextMeshProUGUI text = CreateRect(
            name,
            parent,
            position,
            size
        ).gameObject.AddComponent<TextMeshProUGUI>();

        text.font = TMP_Settings.defaultFontAsset;
        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.raycastTarget = false;
        text.richText = false;

        return text;
    }

    // Adds a button and connects its click to the supplied game action.
    private static Button CreateButton(
        string label,
        Transform parent,
        float y,
        UnityAction action,
        out TMP_Text text)
    {
        RectTransform rect = CreateRect(
            label,
            parent,
            new Vector2(0, y),
            new Vector2(440, 46)
        );

        Image image = rect.gameObject.AddComponent<Image>();
        image.color = new Color(0.16f, 0.45f, 0.43f);

        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(action);

        text = CreateText(
            "Label",
            rect,
            Vector2.zero,
            new Vector2(420, 42),
            22
        );

        text.text = label;

        return button;
    }

    // Removes the generated canvas if this controller's UI is destroyed.
    private void OnDestroy()
    {
        if (canvasObject != null)
        {
            Destroy(canvasObject);
        }
    }
}