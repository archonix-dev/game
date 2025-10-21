using UnityEngine;

public class ObjectInteraction : MonoBehaviour
{
    public float doorSpeed = 2f;
    
    private InteractionUI interactionUI;
    private bool isOpen = false;
    private bool isInteracting = false;
    private Vector3 originalRotation;
    private Vector3 targetRotation;
    private float interactionDistance = 3f;
    private Transform player;
    private string currentDoorSide;

    void Start()
    {
        originalRotation = transform.eulerAngles;
        player = GameObject.FindGameObjectWithTag("Player").transform;
        interactionUI = FindObjectOfType<InteractionUI>();
    }

    void Update()
    {
        if (player == null) return;

        RaycastHit hit;
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out hit, interactionDistance))
        {
            if (hit.collider.CompareTag("leftdoor") || hit.collider.CompareTag("rightdoor"))
            {
                currentDoorSide = hit.collider.tag;
                
                if (interactionUI != null)
                {
                    interactionUI.ShowInteraction(isOpen ? "[E] Закрыть" : "[E] Открыть");
                }

                if (Input.GetKeyDown(KeyCode.E) && !isInteracting)
                {
                    StartInteraction();
                }
            }
            else
            {
                if (interactionUI != null)
                {
                    interactionUI.HideInteraction();
                }
            }
        }
        else
        {
            if (interactionUI != null)
            {
                interactionUI.HideInteraction();
            }
        }

        if (isInteracting)
        {
            transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.Euler(targetRotation), Time.deltaTime * doorSpeed);
            if (Quaternion.Angle(transform.rotation, Quaternion.Euler(targetRotation)) < 1f)
            {
                transform.rotation = Quaternion.Euler(targetRotation);
                isInteracting = false;
            }
        }
    }

    void StartInteraction()
    {
        isInteracting = true;
        isOpen = !isOpen;

        if (isOpen)
        {
            if (currentDoorSide == "leftdoor")
            {
                targetRotation = originalRotation + new Vector3(0, 90, 0);
            }
            else if (currentDoorSide == "rightdoor")
            {
                targetRotation = originalRotation + new Vector3(0, -90, 0);
            }
        }
        else
        {
            targetRotation = originalRotation;
        }

        if (interactionUI != null)
        {
            interactionUI.ShowInteraction(isOpen ? "[E] Закрыть" : "[E] Открыть");
        }
    }
}
