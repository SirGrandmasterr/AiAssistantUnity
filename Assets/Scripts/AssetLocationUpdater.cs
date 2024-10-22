using Unity.VisualScripting;
using UnityEngine;

public class AssetLocationUpdater : MonoBehaviour
{
    // Start is called before the first frame update
    public string location;
    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Location"))
        {
            location = other.GameObject().name;
        }
    }
    // Update is called once per frame
    
}
