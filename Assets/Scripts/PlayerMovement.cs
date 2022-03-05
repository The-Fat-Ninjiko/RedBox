using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI; 


public class PlayerMovement : MonoBehaviour
{
    public Rigidbody rb;
    public float forwardForce = 2000f;
    public float sidewaysforce = 500f;
    
    // Update is called once per frame
    void FixedUpdate ()
    {
        //Add a forward force
        rb.AddForce(0, 0, forwardForce * Time.deltaTime);

        if(rb.position.y < -1f)
            FindObjectOfType<GameManager>().EndGame();
    }

    public void MoveRight()
    {
        rb.AddForce(sidewaysforce * Time.deltaTime, 0, 0, ForceMode.VelocityChange);
    }

    public void MoveLeft()
    {
        rb.AddForce(-sidewaysforce * Time.deltaTime, 0, 0, ForceMode.VelocityChange);
    }
}
