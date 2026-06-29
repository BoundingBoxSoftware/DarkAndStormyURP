using UnityEngine;
using System.Collections;
using UnityEngine.InputSystem;

public class FlyCamera : MonoBehaviour 
{

	public int mouseNavButton = 1;

	//public float forwardSpeed = 10.0f;
    //public float upSpeed = 8.0f;
    //public float strafeSpeed = 8.0f;

	public float smoothness = 30f;

	public float speed = 15.0f;
	public float sprintSpeed = 30.0f;
	public float acceleration = 1.0f;
    public float mouseSpeedX = 30.0f;
    public float mouseSpeedY = 20.0f;
    public float yMinLimit = -60;
    public float yMaxLimit = 60;
    

	public float lookDampening = 20f;
	public float velocityDampening = 20f;
	

    private float lookVertical = 0.0f;
    private float lookHorizontal = 0.0f;

	private Transform targetTransform;

	[HideInInspector]
	public bool navigating = false;

	private float origX = 0.0f;
	private float origY = 0.0f;

	float xVel = 0f;
	float yVel = 0f;
	Vector3 velocity = Vector3.zero;
	
	float mousePosX = 0f;
	float mousePosY = 0f;

    private void Start () 
    {
		targetTransform = new GameObject().transform;
		targetTransform.name = "CameraTargetTransform";
		targetTransform.position = transform.position;
		targetTransform.rotation = transform.rotation;

		Vector3 angles = targetTransform.eulerAngles;

		if( angles.x > 180 ){
			angles.x = Mathf.Repeat(angles.x, 360) - 360;
		}

		lookVertical = ClampAngle(angles.x, yMinLimit, yMaxLimit);
		lookHorizontal = angles.y;

		origX = lookVertical;
		origY = lookHorizontal;

		targetTransform.rotation = Quaternion.Euler(lookVertical, lookHorizontal, 0);
		
		mousePosX = Input.mousePosition.x;
		mousePosY = Input.mousePosition.y;
		
    }

	public void Reset()
	{
		lookVertical = origX;
		lookHorizontal = origY;

		targetTransform.rotation = Quaternion.Euler(lookVertical, lookHorizontal, 0);
		transform.rotation = Quaternion.Euler(lookVertical, lookHorizontal, 0);
	}

    private void LateUpdate () 
    {

	    Cursor.visible = false;
	    Cursor.lockState = CursorLockMode.Locked;
	    
	    float dTime = Time.deltaTime;
	    
	    // dampen velocity
	    xVel -= xVel * lookDampening * dTime;
	    yVel -= yVel * lookDampening * dTime;
	    velocity -= velocity * (velocityDampening * dTime);
	    
	    bool navigationStarted = Input.GetMouseButtonDown(mouseNavButton);
        
        if ( navigating ) {
	        xVel += Input.mousePositionDelta.x * mouseSpeedX * dTime;
	        yVel -= Input.mousePositionDelta.y * mouseSpeedY * dTime;
        }
        
        if (navigationStarted) {
	        navigating = true;
        }
        
        if( !Input.GetMouseButton(mouseNavButton) ) navigating = false;
        
        lookVertical += yVel;
        lookHorizontal += xVel;

        // Clamping
        lookVertical = ClampAngle(lookVertical, yMinLimit, yMaxLimit);
	    lookHorizontal = WrapAngle(lookHorizontal);

		targetTransform.rotation = Quaternion.Euler(lookVertical, lookHorizontal, 0);
		
		float curSpeed = speed;
		if( Input.GetKey(KeyCode.LeftShift) ) curSpeed = sprintSpeed; 
	
		float fAxis = curSpeed * (Input.GetKey(KeyCode.W)		? 1 : (Input.GetKey(KeyCode.S)	? -1 : 0));
		float uAxis = curSpeed * (Input.GetKey(KeyCode.Space)	? 1 : (Input.GetKey(KeyCode.C)	? -1 : 0));
		float sAxis = curSpeed * (Input.GetKey(KeyCode.D)		? 1 : (Input.GetKey(KeyCode.A)	? -1 : 0));
		
		Vector3 targetVelocity = targetTransform.forward * fAxis + targetTransform.right * sAxis + targetTransform.up * uAxis;
		velocity = Vector3.Lerp( velocity, targetVelocity, acceleration * dTime);
		targetTransform.position += velocity * dTime;

		transform.position = Vector3.Lerp( transform.position, targetTransform.position, smoothness );
		transform.rotation = Quaternion.Lerp( transform.rotation, targetTransform.rotation, smoothness );
    }

	float WrapAngle (float angle)
	{
		return Mathf.Repeat(angle, 360);
	}

	float ClampAngle (float angle, float min, float max)
    {
		if( angle > 180 )
			angle = Mathf.Repeat(angle, 360) - 360;

		angle = Mathf.Clamp(angle, min, max);
		return angle;
    }
}
