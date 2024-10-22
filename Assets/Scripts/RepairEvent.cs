using UnityEngine;

public class RepairEvent : MonoBehaviour
{

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("CanBeRepaired"))
        {
            
            print(other.gameObject.name);
            other.gameObject.SetActive(true);
            other.gameObject.transform.GetChild(0).gameObject.SetActive(true);
            
        }
    }

    
}