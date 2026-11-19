using TMPro;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class BinScoreZone : MonoBehaviour
{
    [Header("Scoring")]
    public int score = 0;
    public bool destroyPaperOnScore = false;

    [Header("UI")]
    public TMP_Text scoreText;
    public string scorePrefix = "Score: ";

    private void Start()
    {
        UpdateScoreUI();
    }

    private void Reset()
    {
        // Automatically mark this collider as a trigger when attached
        Collider col = GetComponent<Collider>();
        col.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Paper") || other.GetComponent<Rigidbody>() != null)
        {
            score++;
            UpdateScoreUI();
            Debug.Log($"Score! Total: {score}");

            if (destroyPaperOnScore)
                Destroy(other.gameObject);
        }
    }

    private void UpdateScoreUI()
    {
        if (scoreText != null)
            scoreText.text = $"{scorePrefix}{score}";
    }
}