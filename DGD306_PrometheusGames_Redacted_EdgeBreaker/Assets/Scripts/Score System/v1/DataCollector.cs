using UnityEngine;

public class DataCollector : MonoBehaviour
{
    

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {

            ScoreManagerV2.instance.AddPoint(5);

            
            Destroy(gameObject);
        }
    }
}
