using UnityEngine;

public class ScoreZone : MonoBehaviour
{
    public int pointsPerBasket = 1;
    public float destroyDelay = 1.0f;

    private void OnTriggerEnter(Collider other)
    {
        // check if the object entering the bottom of the bin is tagged "Paper"
        if (other.CompareTag("Paper"))
        {
            Debug.Log("BASKET! +" + pointsPerBasket + " point!");

            // update high score in PlayerPrefs
            int currentScore = PlayerPrefs.GetInt("CurrentScore", 0) + pointsPerBasket;
            PlayerPrefs.SetInt("CurrentScore", currentScore);

            int highScore = PlayerPrefs.GetInt("HighScore", 0);
            if (currentScore > highScore)
            {
                PlayerPrefs.SetInt("HighScore", currentScore);
                Debug.Log("NEW HIGH SCORE: " + currentScore);
            }
            PlayerPrefs.Save();

            // prevent the same paper from triggering twice while sitting in the bin
            other.tag = "Untagged";

            // remove the paper ball after 1 second
            Destroy(other.gameObject, destroyDelay);
        }
    }
}