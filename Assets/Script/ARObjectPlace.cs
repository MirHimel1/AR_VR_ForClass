using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class ARObjectPlace : MonoBehaviour
{
    [SerializeField] private ARRaycastManager raycastManager;
    private bool IsTap = false;

    void Update()
    {
        if (((Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began) || Input.GetMouseButton(0)) && !IsTap)
        {
            if (Input.touchCount > 0)
            {
                Debug.Log("Touch");
                PlaceObj(Input.GetTouch(0).position);
            }
            else if (Input.GetMouseButton(0))
            {
                Debug.Log("Mouse");
                PlaceObj(Input.mousePosition);
            }

            IsTap = true;
        }
    }

    void PlaceObj(Vector2 Pos)
    {
        var RayHit = new List<ARRaycastHit>();
        raycastManager.Raycast(Pos, RayHit, TrackableType.AllTypes);

        if (RayHit.Count > 0)
        {
            Vector3 Position = RayHit[0].pose.position;
            Quaternion Rot = RayHit[0].pose.rotation;

            Instantiate(raycastManager.raycastPrefab, Position, Rot);
            StartCoroutine(Release());
        }
    }

    System.Collections.IEnumerator Release()
    {
        yield return new WaitForSeconds(0.25f);
        IsTap = false;
    }
}