using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class GameUI : MonoBehaviour
{
    public static readonly Color SpeedUpBannerColor = new Color(0.84f, 0.24f, 0.16f, 0.96f);
    public static readonly Color RoundCompleteBannerColor = new Color(0.14f, 0.62f, 0.36f, 0.96f);
    public static readonly Color NightmareBannerColor = new Color(0.55f, 0.10f, 0.28f, 0.96f);

    private static readonly Color[] PlayerColors =
    {
        new Color(0.18f, 0.53f, 0.87f, 1f), // Player 1: Blue
        new Color(0.88f, 0.32f, 0.32f, 1f), // Player 2: Coral Red
        new Color(0.15f, 0.68f, 0.38f, 1f), // Player 3: Emerald Green
        new Color(0.85f, 0.54f, 0.10f, 1f)  // Player 4: Warm Amber
    };

    private static readonly Color CardIdleFill = new Color(0.08f, 0.11f, 0.17f, 1f);
    private static readonly Color OptionIdleBg = new Color(0.09f, 0.12f, 0.18f, 0.92f);
    private static readonly Color OptionWaitingBg = new Color(1f, 1f, 1f, 0.25f);
    private static readonly Color OptionSelectedBg = Color.white;
    private static readonly Color OptionSelectedFg = new Color(0.08f, 0.10f, 0.15f, 1f);

    private class PlayerSlotView
    {
        public Color color;
        public Image outlineImage;
        public Image fillImage;
        public TMP_Text nameText;
        public TMP_Text stateText;

        public Image wasdBg;
        public TMP_Text wasdLabel;

        public Image arrowsBg;
        public TMP_Text arrowsLabel;
        public Image arrowsIcon;

        public Image phoneBg;
        public Image phoneIcon;
    }

    private GameObject canvasObject;
    private GameObject overlay;
    private GameObject hud;
    private GameObject announcementBanner;
    private Image announcementBg;
    private TMP_Text announcementText;
    private float announcementTimer;

    private RectTransform cardRect;
    private GameObject playerSlotsRoot;

    private TMP_Text title;
    private TMP_Text scoreText;
    private TMP_Text details;
    private TMP_Text status;
    private TMP_Text hudText;
    private TMP_Text playLabel;

    private Button playButton;
    private Button menuButton;

    private readonly PlayerSlotView[] slotViews = new PlayerSlotView[GameController.MaxPlayers];
    private readonly List<Object> generatedAssets = new List<Object>();

    private Sprite roundedSquareSprite;
    private Sprite phoneIconSprite;
    private Sprite arrowsIconSprite;
    private bool fontSupportsArrows;

    // Creates the overlay, player setup cards, HUD, and top announcement banner once.
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

        roundedSquareSprite = CreateRoundedRectSprite(64, 16);
        phoneIconSprite = CreatePhoneIconSprite(48);
        fontSupportsArrows = CheckFontSupportsArrows(TMP_Settings.defaultFontAsset);

        if (!fontSupportsArrows)
        {
            arrowsIconSprite = CreateArrowsIconSprite(64, 32);
        }

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

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform hudRect = CreateRect(
            "HUD",
            canvasObject.transform,
            new Vector2(0, -40),
            new Vector2(1080, 56)
        );

        hudRect.anchorMin = hudRect.anchorMax = new Vector2(0.5f, 1f);
        hud = hudRect.gameObject;

        Image hudBackground = hud.AddComponent<Image>();
        hudBackground.color = new Color(0.03f, 0.05f, 0.08f, 0.85f);
        hudBackground.raycastTarget = false;

        hudText = CreateText(
            "HUD Text",
            hudRect,
            Vector2.zero,
            new Vector2(1040, 52),
            21
        );

        RectTransform bannerRect = CreateRect(
            "Announcement Banner",
            canvasObject.transform,
            new Vector2(0, -106),
            new Vector2(560, 52)
        );

        bannerRect.anchorMin = bannerRect.anchorMax = new Vector2(0.5f, 1f);
        announcementBanner = bannerRect.gameObject;

        announcementBg = announcementBanner.AddComponent<Image>();
        announcementBg.sprite = roundedSquareSprite;
        announcementBg.type = Image.Type.Sliced;
        announcementBg.color = SpeedUpBannerColor;
        announcementBg.raycastTarget = false;

        announcementText = CreateText(
            "Announcement Text",
            bannerRect,
            Vector2.zero,
            new Vector2(530, 46),
            26
        );

        announcementText.fontStyle = FontStyles.Bold;
        announcementBanner.SetActive(false);

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
        overlay.AddComponent<Image>().color = new Color(0, 0, 0, 0.55f);

        cardRect = CreateRect(
            "Menu",
            overlayRect,
            Vector2.zero,
            new Vector2(1040, 580)
        );

        Image cardBg = cardRect.gameObject.AddComponent<Image>();
        cardBg.sprite = roundedSquareSprite;
        cardBg.type = Image.Type.Sliced;
        cardBg.color = new Color(0.06f, 0.08f, 0.12f, 0.97f);

        title = CreateText("Title", cardRect, new Vector2(0, 232), new Vector2(960, 54), 40);
        scoreText = CreateText("Score", cardRect, new Vector2(0, 182), new Vector2(960, 34), 24);
        details = CreateText("Details", cardRect, new Vector2(0, 142), new Vector2(960, 44), 17);

        BuildPlayerSlots(controller);

        playButton = CreateButton(
            "Play",
            cardRect,
            -178,
            360,
            46,
            controller.Play,
            out playLabel
        );

        menuButton = CreateButton(
            "Main Menu",
            cardRect,
            -102,
            360,
            46,
            controller.ShowMenu,
            out _
        );

        status = CreateText("Status", cardRect, new Vector2(0, -238), new Vector2(960, 54), 16);
        status.overflowMode = TextOverflowModes.Ellipsis;

        return true;
    }

    private void Update()
    {
        if (announcementBanner == null || !announcementBanner.activeSelf || announcementTimer <= 0f)
            return;

        announcementTimer -= Time.deltaTime;

        if (announcementTimer <= 0f)
        {
            HideAnnouncement();
        }
    }

    // Displays a banner at the top of the screen; durationSeconds <= 0 keeps it visible until hidden.
    public void ShowAnnouncement(string message, Color backgroundColor, float durationSeconds = 2.5f)
    {
        if (announcementBanner == null)
            return;

        announcementText.text = message;
        announcementBg.color = backgroundColor;
        announcementTimer = durationSeconds;
        announcementBanner.SetActive(true);
    }

    public void HideAnnouncement()
    {
        if (announcementBanner == null)
            return;

        announcementTimer = 0f;
        announcementBanner.SetActive(false);
    }

    // Builds the 4 rounded-square player cards in a horizontal row.
    private void BuildPlayerSlots(GameController controller)
    {
        RectTransform container = CreateRect(
            "Player Slots",
            cardRect,
            new Vector2(0, -8),
            new Vector2(952, 224)
        );

        playerSlotsRoot = container.gameObject;

        const float cardSize = 220f;
        const float spacing = 24f;
        float totalWidth = GameController.MaxPlayers * cardSize + (GameController.MaxPlayers - 1) * spacing;
        float startX = -totalWidth * 0.5f + cardSize * 0.5f;

        for (int i = 0; i < GameController.MaxPlayers; i++)
        {
            int slotIndex = i;
            Color playerColor = PlayerColors[i % PlayerColors.Length];
            float x = startX + i * (cardSize + spacing);

            RectTransform outlineRect = CreateRect(
                $"Player {i + 1} Card",
                container,
                new Vector2(x, 0),
                new Vector2(cardSize, cardSize)
            );

            Image outlineImage = outlineRect.gameObject.AddComponent<Image>();
            outlineImage.sprite = roundedSquareSprite;
            outlineImage.type = Image.Type.Sliced;
            outlineImage.color = playerColor;

            RectTransform fillRect = CreateRect(
                "Fill",
                outlineRect,
                Vector2.zero,
                Vector2.zero
            );

            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(5f, 5f);
            fillRect.offsetMax = new Vector2(-5f, -5f);

            Image fillImage = fillRect.gameObject.AddComponent<Image>();
            fillImage.sprite = roundedSquareSprite;
            fillImage.type = Image.Type.Sliced;
            fillImage.color = CardIdleFill;
            fillImage.raycastTarget = false;

            TMP_Text nameText = CreateText(
                "Player Label",
                outlineRect,
                new Vector2(0, 66),
                new Vector2(190, 38),
                24
            );

            nameText.fontStyle = FontStyles.Bold;
            nameText.text = $"Player {i + 1}";

            TMP_Text stateText = CreateText(
                "State Label",
                outlineRect,
                new Vector2(0, 14),
                new Vector2(194, 46),
                15
            );

            stateText.text = "";

            const float btnWidth = 58f;
            const float btnHeight = 40f;
            const float btnY = -66f;
            const float btnStep = 66f;

            Button wasdBtn = CreateOptionButton(
                "WASD",
                outlineRect,
                new Vector2(-btnStep, btnY),
                new Vector2(btnWidth, btnHeight),
                () => controller.SelectWasd(slotIndex),
                out Image wasdBg
            );

            TMP_Text wasdLabel = CreateText(
                "Label",
                wasdBtn.transform,
                Vector2.zero,
                new Vector2(btnWidth - 4, btnHeight - 4),
                13
            );

            wasdLabel.fontStyle = FontStyles.Bold;
            wasdLabel.text = "WASD";

            Button arrowsBtn = CreateOptionButton(
                "Arrows",
                outlineRect,
                new Vector2(0, btnY),
                new Vector2(btnWidth, btnHeight),
                () => controller.SelectArrows(slotIndex),
                out Image arrowsBg
            );

            TMP_Text arrowsLabel = null;
            Image arrowsIcon = null;

            if (fontSupportsArrows)
            {
                arrowsLabel = CreateText(
                    "Label",
                    arrowsBtn.transform,
                    Vector2.zero,
                    new Vector2(btnWidth - 4, btnHeight - 4),
                    13
                );

                arrowsLabel.fontStyle = FontStyles.Bold;
                arrowsLabel.text = "↑←↓→";
            }
            else
            {
                arrowsIcon = CreateIcon(
                    "Arrows Icon",
                    arrowsBtn.transform,
                    new Vector2(46, 22),
                    arrowsIconSprite
                );
            }

            Button phoneBtn = CreateOptionButton(
                "Phone",
                outlineRect,
                new Vector2(btnStep, btnY),
                new Vector2(btnWidth, btnHeight),
                () => controller.SelectPhone(slotIndex),
                out Image phoneBg
            );

            Image phoneIcon = CreateIcon(
                "Phone Icon",
                phoneBtn.transform,
                new Vector2(26, 26),
                phoneIconSprite
            );

            slotViews[i] = new PlayerSlotView
            {
                color = playerColor,
                outlineImage = outlineImage,
                fillImage = fillImage,
                nameText = nameText,
                stateText = stateText,
                wasdBg = wasdBg,
                wasdLabel = wasdLabel,
                arrowsBg = arrowsBg,
                arrowsLabel = arrowsLabel,
                arrowsIcon = arrowsIcon,
                phoneBg = phoneBg,
                phoneIcon = phoneIcon
            };
        }
    }

    // Displays the start menu with the 4 player setup squares.
    public void ShowMenu(int highScore, string instructions, PlayerSlotState[] slots)
    {
        ApplyMenuLayout();

        ShowOverlay(
            "BOSS SURVIVAL",
            $"High score: {highScore}",
            instructions,
            "Play"
        );

        playerSlotsRoot.SetActive(true);
        menuButton.gameObject.SetActive(false);

        UpdatePlayerSlots(slots);
    }

    // Refreshes all 4 player square visuals (outline vs filled, button highlights, waiting text).
    public void UpdatePlayerSlots(PlayerSlotState[] slots)
    {
        if (slots == null)
            return;

        int count = Mathf.Min(slotViews.Length, slots.Length);

        for (int i = 0; i < count; i++)
        {
            PlayerSlotView view = slotViews[i];
            PlayerSlotState slot = slots[i];

            bool isSelected = slot.IsReady;
            view.fillImage.color = isSelected ? view.color : CardIdleFill;
            view.outlineImage.color = view.color;

            switch (slot.mode)
            {
                case PlayerControlMode.Wasd:
                    view.stateText.text = "WASD";
                    break;
                case PlayerControlMode.Arrows:
                    view.stateText.text = "Arrow Keys";
                    break;
                case PlayerControlMode.WaitingForGamepad:
                    view.stateText.text = "Waiting...";
                    break;
                case PlayerControlMode.RemoteGamepad:
                    view.stateText.text = string.IsNullOrEmpty(slot.deviceLabel)
                        ? "Connected"
                        : slot.deviceLabel;
                    break;
                default:
                    view.stateText.text = "";
                    break;
            }

            SetOptionVisual(
                view.wasdBg,
                view.wasdLabel,
                null,
                slot.mode == PlayerControlMode.Wasd,
                false
            );

            SetOptionVisual(
                view.arrowsBg,
                view.arrowsLabel,
                view.arrowsIcon,
                slot.mode == PlayerControlMode.Arrows,
                false
            );

            SetOptionVisual(
                view.phoneBg,
                null,
                view.phoneIcon,
                slot.mode == PlayerControlMode.RemoteGamepad,
                slot.mode == PlayerControlMode.WaitingForGamepad
            );
        }
    }

    private static void SetOptionVisual(
        Image bg,
        TMP_Text label,
        Image icon,
        bool isSelected,
        bool isWaiting)
    {
        bg.color = isSelected
            ? OptionSelectedBg
            : (isWaiting ? OptionWaitingBg : OptionIdleBg);

        Color fg = isSelected ? OptionSelectedFg : Color.white;

        if (label != null)
        {
            label.color = fg;
        }

        if (icon != null)
        {
            icon.color = fg;
        }
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
        ApplyResultLayout();

        ShowOverlay(
            won ? "You win!" : "You lose!",
            $"Score: {score}",
            $"High score: {highScore}\n" +
            $"Survived: {elapsed:0.00}s | Seen: {seen}s",
            "Play Again"
        );

        playerSlotsRoot.SetActive(false);
        menuButton.gameObject.SetActive(true);
    }

    private void ApplyMenuLayout()
    {
        cardRect.sizeDelta = new Vector2(1040, 580);

        title.rectTransform.anchoredPosition = new Vector2(0, 232);
        scoreText.rectTransform.anchoredPosition = new Vector2(0, 182);
        details.rectTransform.anchoredPosition = new Vector2(0, 142);
        details.rectTransform.sizeDelta = new Vector2(960, 44);

        playButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -178);
        status.rectTransform.anchoredPosition = new Vector2(0, -238);
        status.rectTransform.sizeDelta = new Vector2(960, 54);
    }

    private void ApplyResultLayout()
    {
        cardRect.sizeDelta = new Vector2(620, 440);

        title.rectTransform.anchoredPosition = new Vector2(0, 155);
        scoreText.rectTransform.anchoredPosition = new Vector2(0, 100);
        details.rectTransform.anchoredPosition = new Vector2(0, 38);
        details.rectTransform.sizeDelta = new Vector2(570, 78);

        playButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -44);
        menuButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -102);
        status.rectTransform.anchoredPosition = new Vector2(0, -172);
        status.rectTransform.sizeDelta = new Vector2(570, 70);
    }

    private void ShowOverlay(
        string heading,
        string score,
        string description,
        string button)
    {
        HideAnnouncement();

        overlay.SetActive(true);
        hud.SetActive(false);

        title.text = heading;
        scoreText.text = score;
        details.text = description;
        playLabel.text = button;
        status.text = "";

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        EventSystem.current.SetSelectedGameObject(null);
    }

    public void SetStatus(string message)
    {
        status.text = message;
    }

    public void UpdateHud(
        string roundName,
        double remaining,
        float bossSpeed,
        int score,
        bool seen)
    {
        hudText.text =
            $"{roundName}     Time: {remaining:0.0}s     Speed: {bossSpeed:0.#}     Score: {score}     " +
            (seen ? "SEEN" : "HIDDEN");
    }

    private static bool CheckFontSupportsArrows(TMP_FontAsset font)
    {
        return font.HasCharacter('↑', true) &&
               font.HasCharacter('←', true) &&
               font.HasCharacter('↓', true) &&
               font.HasCharacter('→', true);
    }

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
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        return rect;
    }

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

    private static Image CreateIcon(
        string name,
        Transform parent,
        Vector2 size,
        Sprite sprite)
    {
        Image image = CreateRect(
            name,
            parent,
            Vector2.zero,
            size
        ).gameObject.AddComponent<Image>();

        image.sprite = sprite;
        image.preserveAspect = true;
        image.color = Color.white;
        image.raycastTarget = false;

        return image;
    }

    private Button CreateButton(
        string label,
        Transform parent,
        float y,
        float width,
        float height,
        UnityAction action,
        out TMP_Text text)
    {
        RectTransform rect = CreateRect(
            label,
            parent,
            new Vector2(0, y),
            new Vector2(width, height)
        );

        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = roundedSquareSprite;
        image.type = Image.Type.Sliced;
        image.color = new Color(0.16f, 0.45f, 0.43f);

        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        button.onClick.AddListener(action);

        text = CreateText(
            "Label",
            rect,
            Vector2.zero,
            new Vector2(width - 20, height - 4),
            22
        );

        text.text = label;

        return button;
    }

    private Button CreateOptionButton(
        string name,
        Transform parent,
        Vector2 position,
        Vector2 size,
        UnityAction action,
        out Image background)
    {
        RectTransform rect = CreateRect(name, parent, position, size);

        background = rect.gameObject.AddComponent<Image>();
        background.sprite = roundedSquareSprite;
        background.type = Image.Type.Sliced;
        background.color = OptionIdleBg;

        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = background;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        button.onClick.AddListener(action);

        return button;
    }

    private Sprite CreateRoundedRectSprite(int size, float radius)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        float half = size * 0.5f;
        float innerHalf = half - radius;
        Color[] pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            float py = Mathf.Abs(y + 0.5f - half) - innerHalf;

            for (int x = 0; x < size; x++)
            {
                float px = Mathf.Abs(x + 0.5f - half) - innerHalf;
                float outside = Mathf.Sqrt(
                    Mathf.Max(px, 0f) * Mathf.Max(px, 0f) +
                    Mathf.Max(py, 0f) * Mathf.Max(py, 0f)
                );

                float inside = Mathf.Min(Mathf.Max(px, py), 0f);
                float signedDist = outside + inside - radius;
                float alpha = Mathf.Clamp01(0.5f - signedDist);

                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();

        float border = radius + 2f;
        Sprite sprite = Sprite.Create(
            tex,
            new Rect(0, 0, size, size),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect,
            new Vector4(border, border, border, border)
        );

        generatedAssets.Add(tex);
        generatedAssets.Add(sprite);
        return sprite;
    }

    private Sprite CreatePhoneIconSprite(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        Color[] pixels = new Color[size * size];
        float cx = size * 0.5f;
        float cy = size * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float px = x + 0.5f;
                float py = y + 0.5f;

                float outerDist = RoundedBoxSdf(px - cx, py - cy, 11f, 18f, 3.5f);
                float innerDist = RoundedBoxSdf(px - cx, py - cy, 8.5f, 15.5f, 1.5f);

                bool isFrame = outerDist <= 0f && innerDist >= 0f;
                bool isSpeaker = Mathf.Abs(px - cx) <= 3.5f && Mathf.Abs(py - (cy + 12.2f)) <= 0.9f;
                bool isHomeBar = Mathf.Abs(px - cx) <= 3.5f && Mathf.Abs(py - (cy - 12.5f)) <= 1.0f;

                float alpha = (isFrame || isSpeaker || isHomeBar) ? 1f : 0f;
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();

        Sprite sprite = Sprite.Create(
            tex,
            new Rect(0, 0, size, size),
            new Vector2(0.5f, 0.5f),
            100f
        );

        generatedAssets.Add(tex);
        generatedAssets.Add(sprite);
        return sprite;
    }

    private Sprite CreateArrowsIconSprite(int width, int height)
    {
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        Color[] pixels = new Color[width * height];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = Color.clear;
        }

        DrawArrow(pixels, width, height, 8, height / 2, Vector2Int.up);
        DrawArrow(pixels, width, height, 24, height / 2, Vector2Int.left);
        DrawArrow(pixels, width, height, 40, height / 2, Vector2Int.down);
        DrawArrow(pixels, width, height, 56, height / 2, Vector2Int.right);

        tex.SetPixels(pixels);
        tex.Apply();

        Sprite sprite = Sprite.Create(
            tex,
            new Rect(0, 0, width, height),
            new Vector2(0.5f, 0.5f),
            100f
        );

        generatedAssets.Add(tex);
        generatedAssets.Add(sprite);
        return sprite;
    }

    private static void DrawArrow(
        Color[] pixels,
        int width,
        int height,
        int cx,
        int cy,
        Vector2Int dir)
    {
        for (int step = -5; step <= 5; step++)
        {
            int px = cx + dir.x * step;
            int py = cy + dir.y * step;
            SetThickPixel(pixels, width, height, px, py);
        }

        Vector2Int perp = new Vector2Int(-dir.y, dir.x);
        int tipX = cx + dir.x * 5;
        int tipY = cy + dir.y * 5;

        for (int head = 1; head <= 4; head++)
        {
            int bx = tipX - dir.x * head;
            int by = tipY - dir.y * head;

            SetThickPixel(pixels, width, height, bx + perp.x * head, by + perp.y * head);
            SetThickPixel(pixels, width, height, bx - perp.x * head, by - perp.y * head);
        }
    }

    private static void SetThickPixel(Color[] pixels, int width, int height, int x, int y)
    {
        for (int dy = 0; dy <= 1; dy++)
        {
            for (int dx = 0; dx <= 1; dx++)
            {
                int px = x + dx;
                int py = y + dy;

                if (px >= 0 && px < width && py >= 0 && py < height)
                {
                    pixels[py * width + px] = Color.white;
                }
            }
        }
    }

    private static float RoundedBoxSdf(float x, float y, float halfW, float halfH, float radius)
    {
        float qx = Mathf.Abs(x) - (halfW - radius);
        float qy = Mathf.Abs(y) - (halfH - radius);

        float outside = Mathf.Sqrt(
            Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) +
            Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f)
        );

        return outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
    }

    private void OnDestroy()
    {
        if (canvasObject != null)
        {
            Destroy(canvasObject);
        }

        for (int i = 0; i < generatedAssets.Count; i++)
        {
            if (generatedAssets[i] != null)
            {
                Destroy(generatedAssets[i]);
            }
        }

        generatedAssets.Clear();
    }
}