using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    public float speed = 5f;

    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKey(KeyCode.W))
            transform.position += Vector3.forward * speed * Time.deltaTime;

        if (Input.GetKey(KeyCode.S))
            transform.position += Vector3.back * speed * Time.deltaTime;

        if (Input.GetKey(KeyCode.A))
            transform.position += Vector3.left * speed * Time.deltaTime;

        if (Input.GetKey(KeyCode.D))
            transform.position += Vector3.right * speed * Time.deltaTime;
        
        if (Input.GetKeyDown(KeyCode.F))
        {
            Vector3 spawnPosition = transform.position + Vector3.forward * 2f;

            GameObject clone = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            clone.transform.position = spawnPosition;
            clone.name = "Clone";
        }
    }

    
}
