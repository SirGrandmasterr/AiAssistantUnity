using UnityEngine;

public class RepairEvent : MonoBehaviour
{

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log("REPAIR EVENT COLLIDER TRIGGER");
        if (other.gameObject.CompareTag("CanBeRepaired"))
        {
            
            print(other.gameObject.name);
            //other.gameObject.SetActive(true);
            var test = other.gameObject;
            int count = test.transform.childCount;
            for(int i = 0; i < count; i++){
                test.transform.GetChild(i).gameObject.SetActive(true);
            }
        }
    }

    
}