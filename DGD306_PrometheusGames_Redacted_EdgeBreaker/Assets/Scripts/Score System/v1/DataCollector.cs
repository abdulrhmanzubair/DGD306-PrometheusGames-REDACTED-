using UnityEngine;

public class DataCollector : MonoBehaviour
{
    public int dataValue = 100; // Points awarded per data

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            
            if (ScoreManager.Instance != null)
            {
                ScoreManager.Instance.AddScore(dataValue);
            }

            
            Destroy(gameObject);
        }
    }
}
