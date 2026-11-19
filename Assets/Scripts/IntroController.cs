using UnityEngine;
using UnityEngine.UI;

public class IntroController : MonoBehaviour
{
    [Header("UI & Gameplay References")]
    public GameObject introPanel;
    public Button continueButton;
    public PlayerThrower playerThrower;

    [Header("Behavior")]
    public bool pauseTimeDuringIntro = true;

    private void Awake()
    {
        if (introPanel == null)
            introPanel = gameObject;

        if (continueButton != null)
            continueButton.onClick.AddListener(StartGameplay);
    }

    private void Start()
    {
        ShowIntro();
    }

    public void ShowIntro()
    {
        if (introPanel != null)
            introPanel.SetActive(true);

        if (playerThrower != null)
            playerThrower.enabled = false;

        if (pauseTimeDuringIntro)
            Time.timeScale = 0f;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void StartGameplay()
    {
        if (pauseTimeDuringIntro)
            Time.timeScale = 1f;

        if (introPanel != null)
            introPanel.SetActive(false);

        if (playerThrower != null)
            playerThrower.enabled = true;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}