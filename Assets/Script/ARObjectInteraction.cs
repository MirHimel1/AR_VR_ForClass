using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class ARObjectInteraction : MonoBehaviour
{
    // ============================================================
    // REFERENCES
    // ============================================================

    [Header("References")]
    [SerializeField] private Camera arCamera;
    [SerializeField] private ARRaycastManager raycastManager;


    // ============================================================
    // PLACEMENT STATE
    // ============================================================

    [Header("Placement State")]

    [Tooltip("Set TRUE while placing an AR object. Interaction is disabled.")]
    public bool isPlacing = false;


    // ============================================================
    // OBJECT SELECTION
    // ============================================================

    [Header("Object Selection")]

    [SerializeField] private string interactableTag = "ARObject";

    [Tooltip("Tapping empty space (a real tap, not a drag or pinch) deselects.")]
    [SerializeField] private bool deselectWhenTapEmptySpace = true;

    [Tooltip("Ignore touches/clicks that start on UI elements (buttons, etc).")]
    [SerializeField] private bool ignoreUITouches = true;


    // ============================================================
    // MOVEMENT
    // ============================================================

    [Header("Movement")]

    [SerializeField] private bool allowMove = true;

    [SerializeField] private float dragThreshold = 10f;

    [SerializeField] private bool fallbackToHorizontalPlane = true;


    // ============================================================
    // SCALE
    // ============================================================

    [Header("Scale")]

    [SerializeField] private bool allowScale = true;

    [Tooltip("How strongly a pinch changes scale. 1 = follows your fingers exactly.")]
    [SerializeField] private float pinchSensitivity = 1f;

    [Tooltip("Keyboard +/- step (0.1 = 10%).")]
    [SerializeField] private float keyboardScaleStep = 0.1f;

    [SerializeField] private float mouseWheelScaleSpeed = 0.1f;

    [SerializeField] private float minScale = 0.2f;

    [SerializeField] private float maxScale = 3f;


    // ============================================================
    // ROTATION
    // ============================================================

    [Header("Rotation")]

    [SerializeField] private bool allowRotation = true;

    [SerializeField] private float rotationSpeed = 1f;

    [Tooltip("Ignore twist smaller than this many degrees per frame.")]
    [SerializeField] private float rotationDeadZone = 0.05f;

    [SerializeField] private float keyboardRotationSpeed = 90f;


    // ============================================================
    // GIZMO SHAPE
    // ============================================================

    public enum GizmoShape
    {
        None,
        Box,
        Sphere,
        BoxAndSphere
    }


    [Header("Runtime Gizmo")]

    [SerializeField] private bool showGizmo = true;

    [SerializeField] private GizmoShape gizmoShape = GizmoShape.Box;


    // ============================================================
    // GIZMO SCALE
    // ============================================================

    [Header("Gizmo Scale")]

    [Min(0.01f)]
    [SerializeField] private float gizmoScale = 1f;


    // ============================================================
    // GIZMO COLORS
    // ============================================================

    [Header("Gizmo Colors")]

    [SerializeField] private Color gizmoColor = new Color(1f, 1f, 1f, 0.9f);
    [SerializeField] private Color xAxisColor = Color.red;
    [SerializeField] private Color yAxisColor = Color.green;
    [SerializeField] private Color zAxisColor = Color.blue;
    [SerializeField] private Color rotationRingColor = Color.white;


    // ============================================================
    // GIZMO SIZE
    // ============================================================

    [Header("Gizmo Size")]

    [Min(0.0001f)]
    [SerializeField] private float gizmoLineWidth = 0.008f;

    [Min(0f)]
    [SerializeField] private float gizmoPadding = 0.05f;

    [Min(0f)]
    [SerializeField] private float axisLength = 0.25f;

    [Min(0.01f)]
    [SerializeField] private float sphereRadius = 1f;

    [Range(8, 128)]
    [SerializeField] private int sphereSegments = 48;

    [Min(0.01f)]
    [SerializeField] private float rotationRingRadius = 1.2f;

    [Range(16, 128)]
    [SerializeField] private int rotationRingSegments = 64;


    // ============================================================
    // SELECTED OBJECT
    // ============================================================

    private GameObject selectedObject;

    private Vector3 selectedObjectOriginalScale;

    private float currentScaleMultiplier = 1f;


    // ============================================================
    // TOUCH STATE
    // ============================================================

    private bool isTouchDragging;

    private Vector2 touchStartPosition;

    private Vector3 touchDragOffset;

    // The current touch began ON the selected object, so dragging moves it
    private bool touchControlsObject;

    // The current touch began on empty space (candidate for deselect-on-tap)
    private bool touchBeganOnEmpty;

    // Ignore the remaining finger after a pinch/twist (or a UI touch)
    // until ALL fingers are lifted. Prevents the object jumping.
    private bool touchLocked;


    // ============================================================
    // MOUSE STATE
    // ============================================================

    private bool isMouseDragging;

    private Vector2 mouseStartPosition;

    private Vector3 mouseDragOffset;

    private bool mouseControlsObject;

    private bool mouseBeganOnEmpty;

    private bool mouseLocked;


    // ============================================================
    // TWO FINGER STATE
    // ============================================================

    private bool twoFingerGestureActive;

    private float previousTouchDistance;

    private float previousTouchAngle;


    // ============================================================
    // AR RAYCAST
    // ============================================================

    private static readonly List<ARRaycastHit> arHits = new List<ARRaycastHit>();


    // ============================================================
    // GIZMO OBJECTS
    // ============================================================

    private GameObject gizmoRoot;

    private LineRenderer boundingBox;

    private LineRenderer sphereHorizontal;
    private LineRenderer sphereVertical;
    private LineRenderer sphereDepth;

    private LineRenderer xAxis;
    private LineRenderer yAxis;
    private LineRenderer zAxis;

    private LineRenderer rotationRing;


    // ============================================================
    // GIZMO MATERIALS
    // ============================================================

    private Material boxMaterial;
    private Material sphereMaterial;

    private Material xAxisMaterial;
    private Material yAxisMaterial;
    private Material zAxisMaterial;

    private Material rotationRingMaterial;


    // ============================================================
    // INTERACTION MODE (kept for UI / API compatibility)
    // ============================================================

    private enum InteractionMode
    {
        Move,
        Rotate,
        Scale
    }

    private InteractionMode currentMode = InteractionMode.Move;


    // ============================================================
    // AWAKE
    // ============================================================

    private void Awake()
    {
        if (arCamera == null)
            arCamera = Camera.main;

        if (raycastManager == null)
            raycastManager = FindFirstObjectByType<ARRaycastManager>();

        CreateGizmo();
    }


    // ============================================================
    // UPDATE
    // ============================================================

    private void Update()
    {
        // DISABLE INTERACTION WHILE PLACING
        if (isPlacing)
        {
            ResetInputState();
            HideGizmo();
            return;
        }

        HandleTouchInput();

        HandleMouseInput();

        HandleKeyboardInput();

        // UPDATE GIZMO
        if (selectedObject != null && showGizmo)
            UpdateGizmo();
        else
            HideGizmo();
    }


    // ============================================================
    // UI CHECK
    // ============================================================

    private bool IsPointerOverUI(int fingerId)
    {
        if (!ignoreUITouches || EventSystem.current == null)
            return false;

        // fingerId < 0 -> mouse
        if (fingerId < 0)
            return EventSystem.current.IsPointerOverGameObject();

        return EventSystem.current.IsPointerOverGameObject(fingerId);
    }


    // ============================================================
    // TOUCH INPUT
    // ============================================================

    private void HandleTouchInput()
    {
        // --------------------------------------------------------
        // NO TOUCHES -> RESET EVERYTHING
        // --------------------------------------------------------

        if (Input.touchCount == 0)
        {
            isTouchDragging = false;
            touchControlsObject = false;
            touchBeganOnEmpty = false;
            touchLocked = false;

            twoFingerGestureActive = false;
            previousTouchDistance = 0f;
            previousTouchAngle = 0f;

            return;
        }


        // --------------------------------------------------------
        // TWO (OR MORE) FINGERS -> PINCH + TWIST
        // --------------------------------------------------------

        if (Input.touchCount >= 2)
        {
            isTouchDragging = false;
            touchControlsObject = false;
            touchBeganOnEmpty = false;   // a pinch is never a "tap to deselect"
            touchLocked = true;

            HandleTwoFingerGesture();

            return;
        }


        // --------------------------------------------------------
        // ONE FINGER
        // --------------------------------------------------------

        twoFingerGestureActive = false;

        // Leftover finger after a pinch (or touch that started on UI):
        // ignore it until every finger is lifted.
        if (touchLocked)
            return;

        Touch touch = Input.GetTouch(0);


        // BEGAN
        if (touch.phase == TouchPhase.Began)
        {
            if (IsPointerOverUI(touch.fingerId))
            {
                touchLocked = true;
                return;
            }

            touchStartPosition = touch.position;

            isTouchDragging = false;

            GameObject hitObject = GetSelectableObject(touch.position);

            if (hitObject != null)
            {
                SelectObject(hitObject);

                touchControlsObject = true;
                touchBeganOnEmpty = false;

                TryGetDragOffset(touch.position, out touchDragOffset);
            }
            else
            {
                touchControlsObject = false;
                touchBeganOnEmpty = true;
            }

            return;
        }


        // MOVED / STATIONARY
        if (touch.phase == TouchPhase.Moved ||
            touch.phase == TouchPhase.Stationary)
        {
            // Only drag if this touch started on the selected object
            if (selectedObject == null || !touchControlsObject)
                return;

            if (!isTouchDragging)
            {
                float movement = Vector2.Distance(touch.position, touchStartPosition);

                if (movement >= dragThreshold)
                    isTouchDragging = true;
                else
                    return;
            }

            if (allowMove)
                MoveSelectedObject(touch.position, touchDragOffset);

            return;
        }


        // ENDED
        if (touch.phase == TouchPhase.Ended ||
            touch.phase == TouchPhase.Canceled)
        {
            // Deselect only on a real tap on empty space
            if (touch.phase == TouchPhase.Ended &&
                touchBeganOnEmpty &&
                deselectWhenTapEmptySpace &&
                Vector2.Distance(touch.position, touchStartPosition) < dragThreshold)
            {
                DeselectObject();
            }

            isTouchDragging = false;
            touchControlsObject = false;
            touchBeganOnEmpty = false;
        }
    }


    // ============================================================
    // TWO FINGER SCALE + ROTATION
    // ============================================================

    private void HandleTwoFingerGesture()
    {
        Touch touch0 = Input.GetTouch(0);
        Touch touch1 = Input.GetTouch(1);

        Vector2 position0 = touch0.position;
        Vector2 position1 = touch1.position;


        // Ignore gestures that start on UI
        if ((touch0.phase == TouchPhase.Began && IsPointerOverUI(touch0.fingerId)) ||
            (touch1.phase == TouchPhase.Began && IsPointerOverUI(touch1.fingerId)))
        {
            return;
        }


        // --------------------------------------------------------
        // NOTHING SELECTED YET?
        // Fingers often land a little off the object, or both land
        // in the same frame. Try to grab the object under either
        // finger or between them.
        // --------------------------------------------------------

        if (selectedObject == null)
        {
            GameObject target = GetSelectableObject(position0);

            if (target == null)
                target = GetSelectableObject(position1);

            if (target == null)
                target = GetSelectableObject((position0 + position1) * 0.5f);

            if (target == null)
                return;

            SelectObject(target);
        }


        // A finger is lifting -> end the gesture cleanly
        if (touch0.phase == TouchPhase.Ended ||
            touch0.phase == TouchPhase.Canceled ||
            touch1.phase == TouchPhase.Ended ||
            touch1.phase == TouchPhase.Canceled)
        {
            twoFingerGestureActive = false;
            return;
        }


        // DISTANCE + ANGLE
        Vector2 direction = position1 - position0;

        float currentDistance = direction.magnitude;

        float currentAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;


        // INITIALIZE
        if (!twoFingerGestureActive)
        {
            twoFingerGestureActive = true;

            previousTouchDistance = currentDistance;
            previousTouchAngle = currentAngle;

            return;
        }


        // PINCH SCALE (multiplicative)
        if (allowScale &&
            previousTouchDistance > 1f &&
            currentDistance > 1f)
        {
            float scaleRatio = currentDistance / previousTouchDistance;

            scaleRatio = Mathf.Pow(scaleRatio, pinchSensitivity);

            ApplyScaleMultiplier(scaleRatio);
        }


        // TWIST ROTATION
        if (allowRotation)
        {
            float angleDelta = Mathf.DeltaAngle(previousTouchAngle, currentAngle);

            if (Mathf.Abs(angleDelta) > rotationDeadZone)
            {
                selectedObject.transform.Rotate(
                    Vector3.up,
                    -angleDelta * rotationSpeed,
                    Space.World);
            }
        }


        previousTouchDistance = currentDistance;
        previousTouchAngle = currentAngle;
    }


    // ============================================================
    // MOUSE INPUT
    // ============================================================

    private void HandleMouseInput()
    {
        if (Input.touchCount > 0)
            return;


        // MOUSE DOWN
        if (Input.GetMouseButtonDown(0))
        {
            if (IsPointerOverUI(-1))
            {
                mouseLocked = true;
                return;
            }

            mouseLocked = false;

            Vector2 mousePosition = Input.mousePosition;

            mouseStartPosition = mousePosition;

            isMouseDragging = false;

            GameObject hitObject = GetSelectableObject(mousePosition);

            if (hitObject != null)
            {
                SelectObject(hitObject);

                mouseControlsObject = true;
                mouseBeganOnEmpty = false;

                TryGetDragOffset(mousePosition, out mouseDragOffset);
            }
            else
            {
                mouseControlsObject = false;
                mouseBeganOnEmpty = true;
            }
        }


        // MOUSE DRAG
        if (Input.GetMouseButton(0) && !mouseLocked)
        {
            if (selectedObject != null && mouseControlsObject)
            {
                float movement = Vector2.Distance(
                    (Vector2)Input.mousePosition,
                    mouseStartPosition);

                if (!isMouseDragging && movement >= dragThreshold)
                    isMouseDragging = true;

                if (isMouseDragging && allowMove)
                    MoveSelectedObject(Input.mousePosition, mouseDragOffset);
            }
        }


        // MOUSE UP
        if (Input.GetMouseButtonUp(0))
        {
            if (!mouseLocked &&
                mouseBeganOnEmpty &&
                deselectWhenTapEmptySpace &&
                Vector2.Distance((Vector2)Input.mousePosition, mouseStartPosition) < dragThreshold)
            {
                DeselectObject();
            }

            isMouseDragging = false;
            mouseControlsObject = false;
            mouseBeganOnEmpty = false;
            mouseLocked = false;
        }


        // MOUSE WHEEL SCALE
        float wheel = Input.mouseScrollDelta.y;

        if (Mathf.Abs(wheel) > 0.001f &&
            selectedObject != null &&
            allowScale)
        {
            ApplyScaleMultiplier(1f + wheel * mouseWheelScaleSpeed);
        }
    }


    // ============================================================
    // KEYBOARD
    // ============================================================

    private void HandleKeyboardInput()
    {
        if (selectedObject == null)
            return;

        if (Input.GetKeyDown(KeyCode.M))
            currentMode = InteractionMode.Move;

        if (Input.GetKeyDown(KeyCode.R))
            currentMode = InteractionMode.Rotate;

        if (Input.GetKeyDown(KeyCode.S))
            currentMode = InteractionMode.Scale;

        // ROTATION
        if (allowRotation)
        {
            if (Input.GetKey(KeyCode.Q) || Input.GetKey(KeyCode.LeftArrow))
            {
                selectedObject.transform.Rotate(
                    Vector3.up,
                    -keyboardRotationSpeed * Time.deltaTime,
                    Space.World);
            }

            if (Input.GetKey(KeyCode.E) || Input.GetKey(KeyCode.RightArrow))
            {
                selectedObject.transform.Rotate(
                    Vector3.up,
                    keyboardRotationSpeed * Time.deltaTime,
                    Space.World);
            }
        }

        // SCALE
        if (allowScale)
        {
            if (Input.GetKeyDown(KeyCode.Equals) || Input.GetKeyDown(KeyCode.KeypadPlus))
                ApplyScaleMultiplier(1f + keyboardScaleStep);

            if (Input.GetKeyDown(KeyCode.Minus) || Input.GetKeyDown(KeyCode.KeypadMinus))
                ApplyScaleMultiplier(1f - keyboardScaleStep);
        }
    }


    // ============================================================
    // SELECT OBJECT
    // ============================================================

    private void SelectObject(GameObject target)
    {
        if (target == null)
            return;

        if (selectedObject == target)
        {
            UpdateCurrentScaleMultiplier();
            return;
        }

        selectedObject = target;

        selectedObjectOriginalScale = selectedObject.transform.localScale;

        currentScaleMultiplier = 1f;

        UpdateGizmo();
    }


    // ============================================================
    // DESELECT OBJECT
    // ============================================================

    public void DeselectObject()
    {
        selectedObject = null;

        isTouchDragging = false;
        isMouseDragging = false;

        touchControlsObject = false;
        mouseControlsObject = false;

        twoFingerGestureActive = false;
        previousTouchDistance = 0f;
        previousTouchAngle = 0f;

        HideGizmo();
    }


    // ============================================================
    // GET SELECTABLE OBJECT
    // ============================================================

    private GameObject GetSelectableObject(Vector2 screenPosition)
    {
        if (arCamera == null)
            return null;

        Ray ray = arCamera.ScreenPointToRay(screenPosition);

        // RaycastAll so an untagged collider (e.g. an AR plane collider)
        // can't block selection of the tagged object.
        RaycastHit[] hits = Physics.RaycastAll(ray);

        if (hits.Length == 0)
            return null;

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            Transform current = hit.collider.transform;

            while (current != null)
            {
                if (current.CompareTag(interactableTag))
                    return current.gameObject;

                current = current.parent;
            }
        }

        return null;
    }


    // ============================================================
    // DRAG OFFSET
    // ============================================================

    private bool TryGetDragOffset(Vector2 screenPosition, out Vector3 offset)
    {
        offset = Vector3.zero;

        if (selectedObject == null)
            return false;

        if (TryGetPlanePosition(screenPosition, out Vector3 hitPosition))
        {
            offset = selectedObject.transform.position - hitPosition;
            return true;
        }

        // FALLBACK HORIZONTAL PLANE
        if (fallbackToHorizontalPlane && arCamera != null)
        {
            Plane plane = new Plane(Vector3.up, selectedObject.transform.position);

            Ray ray = arCamera.ScreenPointToRay(screenPosition);

            if (plane.Raycast(ray, out float enter))
            {
                Vector3 hit = ray.GetPoint(enter);

                offset = selectedObject.transform.position - hit;

                return true;
            }
        }

        return false;
    }


    // ============================================================
    // MOVE OBJECT
    // ============================================================

    private void MoveSelectedObject(Vector2 screenPosition, Vector3 offset)
    {
        if (selectedObject == null)
            return;

        if (TryGetPlanePosition(screenPosition, out Vector3 position))
        {
            selectedObject.transform.position = position + offset;
            return;
        }

        if (fallbackToHorizontalPlane && arCamera != null)
        {
            Plane plane = new Plane(Vector3.up, selectedObject.transform.position);

            Ray ray = arCamera.ScreenPointToRay(screenPosition);

            if (plane.Raycast(ray, out float enter))
            {
                Vector3 hit = ray.GetPoint(enter);

                selectedObject.transform.position = hit + offset;
            }
        }
    }


    // ============================================================
    // AR PLANE POSITION
    // ============================================================

    private bool TryGetPlanePosition(Vector2 screenPosition, out Vector3 position)
    {
        position = Vector3.zero;

        if (raycastManager == null)
            return false;

        if (raycastManager.Raycast(
            screenPosition,
            arHits,
            TrackableType.PlaneWithinPolygon))
        {
            position = arHits[0].pose.position;
            return true;
        }

        return false;
    }


    // ============================================================
    // SCALE
    // ============================================================

    private void ApplyScaleMultiplier(float multiplier)
    {
        if (selectedObject == null)
            return;

        if (multiplier <= 0f)
            return;

        currentScaleMultiplier *= multiplier;

        currentScaleMultiplier = Mathf.Clamp(
            currentScaleMultiplier,
            minScale,
            maxScale);

        selectedObject.transform.localScale =
            selectedObjectOriginalScale * currentScaleMultiplier;
    }


    private void UpdateCurrentScaleMultiplier()
    {
        if (selectedObject == null)
            return;

        if (Mathf.Abs(selectedObjectOriginalScale.x) < 0.0001f)
        {
            currentScaleMultiplier = 1f;
            return;
        }

        currentScaleMultiplier =
            selectedObject.transform.localScale.x /
            selectedObjectOriginalScale.x;

        currentScaleMultiplier = Mathf.Clamp(
            currentScaleMultiplier,
            minScale,
            maxScale);
    }


    // ============================================================
    // RESET INPUT
    // ============================================================

    private void ResetInputState()
    {
        isTouchDragging = false;
        isMouseDragging = false;

        touchControlsObject = false;
        touchBeganOnEmpty = false;
        touchLocked = false;

        mouseControlsObject = false;
        mouseBeganOnEmpty = false;
        mouseLocked = false;

        twoFingerGestureActive = false;

        previousTouchDistance = 0f;
        previousTouchAngle = 0f;
    }


    // ============================================================
    // CREATE GIZMO
    // ============================================================

    private void CreateGizmo()
    {
        if (gizmoRoot != null)
            return;

        gizmoRoot = new GameObject("Runtime_Transform_Gizmo");

        gizmoRoot.transform.SetParent(transform, false);

        // MATERIALS
        boxMaterial = CreateGizmoMaterial("Gizmo_Box_Material", gizmoColor);
        sphereMaterial = CreateGizmoMaterial("Gizmo_Sphere_Material", gizmoColor);
        xAxisMaterial = CreateGizmoMaterial("Gizmo_X_Material", xAxisColor);
        yAxisMaterial = CreateGizmoMaterial("Gizmo_Y_Material", yAxisColor);
        zAxisMaterial = CreateGizmoMaterial("Gizmo_Z_Material", zAxisColor);
        rotationRingMaterial = CreateGizmoMaterial("Gizmo_Rotation_Material", rotationRingColor);

        // LINE RENDERERS
        boundingBox = CreateLineRenderer("BoundingBox", boxMaterial);
        sphereHorizontal = CreateLineRenderer("SphereHorizontal", sphereMaterial);
        sphereVertical = CreateLineRenderer("SphereVertical", sphereMaterial);
        sphereDepth = CreateLineRenderer("SphereDepth", sphereMaterial);
        xAxis = CreateLineRenderer("XAxis", xAxisMaterial);
        yAxis = CreateLineRenderer("YAxis", yAxisMaterial);
        zAxis = CreateLineRenderer("ZAxis", zAxisMaterial);
        rotationRing = CreateLineRenderer("RotationRing", rotationRingMaterial);

        HideGizmo();
    }


    // ============================================================
    // CREATE LINE RENDERER
    // ============================================================

    private LineRenderer CreateLineRenderer(string objectName, Material material)
    {
        GameObject lineObject = new GameObject(objectName);

        lineObject.transform.SetParent(gizmoRoot.transform, false);

        LineRenderer line = lineObject.AddComponent<LineRenderer>();

        line.useWorldSpace = true;
        line.loop = false;

        line.widthMultiplier = gizmoLineWidth;

        line.numCapVertices = 4;
        line.numCornerVertices = 4;

        if (material != null)
            line.sharedMaterial = material;

        // Material holds the real color; keep the line white so it isn't multiplied.
        line.startColor = Color.white;
        line.endColor = Color.white;

        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;

        line.alignment = LineAlignment.View;
        line.textureMode = LineTextureMode.Stretch;

        line.positionCount = 0;
        line.enabled = false;

        return line;
    }


    // ============================================================
    // CREATE MATERIAL
    // ============================================================

    private Material CreateGizmoMaterial(string materialName, Color color)
    {
        // IMPORTANT FOR MOBILE BUILDS:
        // Shader.Find only works for shaders that are included in the build.
        // "Sprites/Default" is always included, so try it FIRST. The URP/Unlit
        // and Unlit/Color shaders are often stripped on device, which made
        // the gizmo invisible in builds.
        Shader shader = Shader.Find("Sprites/Default");

        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Unlit");

        if (shader == null)
            shader = Shader.Find("Unlit/Color");

        if (shader == null)
        {
            Debug.LogError("ARObjectInteraction: Could not find a suitable gizmo shader.");
            return null;
        }

        Material material = new Material(shader);

        material.name = materialName;

        SetMaterialColor(material, color);

        material.renderQueue = 3000;

        return material;
    }


    // ============================================================
    // SET MATERIAL COLOR
    // ============================================================

    private void SetMaterialColor(Material material, Color color)
    {
        if (material == null)
            return;

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);

        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);
    }


    // ============================================================
    // UPDATE GIZMO
    // ============================================================

    private void UpdateGizmo()
    {
        if (!showGizmo ||
            selectedObject == null ||
            gizmoShape == GizmoShape.None)
        {
            HideGizmo();
            return;
        }

        // MATERIAL COLORS
        SetMaterialColor(boxMaterial, gizmoColor);
        SetMaterialColor(sphereMaterial, gizmoColor);
        SetMaterialColor(xAxisMaterial, xAxisColor);
        SetMaterialColor(yAxisMaterial, yAxisColor);
        SetMaterialColor(zAxisMaterial, zAxisColor);
        SetMaterialColor(rotationRingMaterial, rotationRingColor);

        // BOUNDS
        Bounds bounds = CalculateSelectedLocalBounds();

        Vector3 center = bounds.center;

        Vector3 extents = bounds.extents;

        extents += Vector3.one * gizmoPadding;

        extents *= Mathf.Max(0.01f, gizmoScale);

        // SHAPE
        bool useBox =
            gizmoShape == GizmoShape.Box ||
            gizmoShape == GizmoShape.BoxAndSphere;

        bool useSphere =
            gizmoShape == GizmoShape.Sphere ||
            gizmoShape == GizmoShape.BoxAndSphere;

        if (useBox)
        {
            UpdateBoundingBox(center, extents);
        }
        else
        {
            boundingBox.enabled = false;
        }

        if (useSphere)
        {
            UpdateSphere(center, extents);
        }
        else
        {
            sphereHorizontal.enabled = false;
            sphereVertical.enabled = false;
            sphereDepth.enabled = false;
        }

        UpdateAxes(center, extents);

        UpdateRotationRing(center, extents);

        UpdateLineWidth();
    }


    // ============================================================
    // CALCULATE OBJECT BOUNDS (in selected object's local space)
    // ============================================================

    private Bounds CalculateSelectedLocalBounds()
    {
        bool initialized = false;

        Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);

        Renderer[] renderers = selectedObject.GetComponentsInChildren<Renderer>();

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null)
                continue;

            // Never include our own gizmo lines
            if (gizmoRoot != null &&
                renderer.transform.IsChildOf(gizmoRoot.transform))
            {
                continue;
            }

            Bounds localBounds = renderer.localBounds;

            Vector3 min = localBounds.min;
            Vector3 max = localBounds.max;

            Vector3[] corners =
            {
                new Vector3(min.x, min.y, min.z),
                new Vector3(max.x, min.y, min.z),
                new Vector3(min.x, max.y, min.z),
                new Vector3(max.x, max.y, min.z),

                new Vector3(min.x, min.y, max.z),
                new Vector3(max.x, min.y, max.z),
                new Vector3(min.x, max.y, max.z),
                new Vector3(max.x, max.y, max.z)
            };

            foreach (Vector3 corner in corners)
            {
                Vector3 worldPoint = renderer.transform.TransformPoint(corner);

                Vector3 localPoint =
                    selectedObject.transform.InverseTransformPoint(worldPoint);

                if (!initialized)
                {
                    bounds = new Bounds(localPoint, Vector3.zero);
                    initialized = true;
                }
                else
                {
                    bounds.Encapsulate(localPoint);
                }
            }
        }

        // COLLIDER FALLBACK
        if (!initialized)
        {
            Collider[] colliders = selectedObject.GetComponentsInChildren<Collider>();

            foreach (Collider collider in colliders)
            {
                if (collider == null)
                    continue;

                Bounds worldBounds = collider.bounds;

                Vector3 min = worldBounds.min;
                Vector3 max = worldBounds.max;

                Vector3[] corners =
                {
                    new Vector3(min.x, min.y, min.z),
                    new Vector3(max.x, min.y, min.z),
                    new Vector3(min.x, max.y, min.z),
                    new Vector3(max.x, max.y, min.z),

                    new Vector3(min.x, min.y, max.z),
                    new Vector3(max.x, min.y, max.z),
                    new Vector3(min.x, max.y, max.z),
                    new Vector3(max.x, max.y, max.z)
                };

                foreach (Vector3 corner in corners)
                {
                    Vector3 localPoint =
                        selectedObject.transform.InverseTransformPoint(corner);

                    if (!initialized)
                    {
                        bounds = new Bounds(localPoint, Vector3.zero);
                        initialized = true;
                    }
                    else
                    {
                        bounds.Encapsulate(localPoint);
                    }
                }
            }
        }

        // DEFAULT
        if (!initialized)
            bounds = new Bounds(Vector3.zero, Vector3.one);

        return bounds;
    }


    // ============================================================
    // UPDATE BOX
    // ============================================================

    private void UpdateBoundingBox(Vector3 center, Vector3 extents)
    {
        Vector3 min = center - extents;
        Vector3 max = center + extents;

        Vector3[] points =
        {
            // Bottom
            new Vector3(min.x, min.y, min.z),
            new Vector3(max.x, min.y, min.z),
            new Vector3(max.x, min.y, max.z),
            new Vector3(min.x, min.y, max.z),
            new Vector3(min.x, min.y, min.z),

            // Top
            new Vector3(min.x, max.y, min.z),
            new Vector3(max.x, max.y, min.z),
            new Vector3(max.x, max.y, max.z),
            new Vector3(min.x, max.y, max.z),
            new Vector3(min.x, max.y, min.z),

            // Vertical edges
            new Vector3(min.x, min.y, min.z),
            new Vector3(max.x, min.y, min.z),
            new Vector3(max.x, max.y, min.z),
            new Vector3(max.x, min.y, max.z),
            new Vector3(max.x, max.y, max.z),
            new Vector3(min.x, min.y, max.z),
            new Vector3(min.x, max.y, max.z)
        };

        SetLineFromSelectedLocal(boundingBox, points);

        boundingBox.enabled = true;
    }


    // ============================================================
    // UPDATE SPHERE
    // ============================================================

    private void UpdateSphere(Vector3 center, Vector3 extents)
    {
        float radius =
            Mathf.Max(extents.x, Mathf.Max(extents.y, extents.z)) *
            sphereRadius;

        radius = Mathf.Max(radius, 0.001f);

        Vector3[] horizontal = new Vector3[sphereSegments + 1];
        Vector3[] vertical = new Vector3[sphereSegments + 1];
        Vector3[] depth = new Vector3[sphereSegments + 1];

        for (int i = 0; i <= sphereSegments; i++)
        {
            float angle = ((float)i / sphereSegments) * Mathf.PI * 2f;

            float c = Mathf.Cos(angle) * radius;
            float s = Mathf.Sin(angle) * radius;

            horizontal[i] = center + new Vector3(c, 0f, s);   // XZ
            vertical[i] = center + new Vector3(c, s, 0f);     // XY
            depth[i] = center + new Vector3(0f, c, s);        // YZ
        }

        SetLineFromSelectedLocal(sphereHorizontal, horizontal);
        SetLineFromSelectedLocal(sphereVertical, vertical);
        SetLineFromSelectedLocal(sphereDepth, depth);

        sphereHorizontal.enabled = true;
        sphereVertical.enabled = true;
        sphereDepth.enabled = true;
    }


    // ============================================================
    // UPDATE AXES
    // ============================================================

    private void UpdateAxes(Vector3 center, Vector3 extents)
    {
        float largestExtent = Mathf.Max(extents.x, Mathf.Max(extents.y, extents.z));

        float length = largestExtent + axisLength;

        SetLineFromSelectedLocal(xAxis, new Vector3[] { center, center + Vector3.right * length });
        SetLineFromSelectedLocal(yAxis, new Vector3[] { center, center + Vector3.up * length });
        SetLineFromSelectedLocal(zAxis, new Vector3[] { center, center + Vector3.forward * length });

        xAxis.enabled = true;
        yAxis.enabled = true;
        zAxis.enabled = true;
    }


    // ============================================================
    // UPDATE ROTATION RING
    // ============================================================

    private void UpdateRotationRing(Vector3 center, Vector3 extents)
    {
        float radius = Mathf.Max(extents.x, extents.z) * rotationRingRadius;

        radius = Mathf.Max(radius, 0.001f);

        Vector3[] points = new Vector3[rotationRingSegments + 1];

        for (int i = 0; i <= rotationRingSegments; i++)
        {
            float angle = ((float)i / rotationRingSegments) * Mathf.PI * 2f;

            points[i] = center + new Vector3(
                Mathf.Cos(angle) * radius,
                0f,
                Mathf.Sin(angle) * radius);
        }

        SetLineFromSelectedLocal(rotationRing, points);

        rotationRing.enabled = true;
    }


    // ============================================================
    // LOCAL TO WORLD
    // ============================================================

    private void SetLineFromSelectedLocal(LineRenderer line, Vector3[] localPoints)
    {
        if (line == null || selectedObject == null || localPoints == null)
            return;

        Vector3[] worldPoints = new Vector3[localPoints.Length];

        for (int i = 0; i < localPoints.Length; i++)
        {
            worldPoints[i] = selectedObject.transform.TransformPoint(localPoints[i]);
        }

        line.positionCount = worldPoints.Length;

        line.SetPositions(worldPoints);
    }


    // ============================================================
    // LINE WIDTH
    // ============================================================

    private void UpdateLineWidth()
    {
        if (boundingBox != null) boundingBox.widthMultiplier = gizmoLineWidth;
        if (sphereHorizontal != null) sphereHorizontal.widthMultiplier = gizmoLineWidth;
        if (sphereVertical != null) sphereVertical.widthMultiplier = gizmoLineWidth;
        if (sphereDepth != null) sphereDepth.widthMultiplier = gizmoLineWidth;
        if (xAxis != null) xAxis.widthMultiplier = gizmoLineWidth;
        if (yAxis != null) yAxis.widthMultiplier = gizmoLineWidth;
        if (zAxis != null) zAxis.widthMultiplier = gizmoLineWidth;
        if (rotationRing != null) rotationRing.widthMultiplier = gizmoLineWidth;
    }


    // ============================================================
    // HIDE GIZMO
    // ============================================================

    private void HideGizmo()
    {
        if (boundingBox != null) boundingBox.enabled = false;
        if (sphereHorizontal != null) sphereHorizontal.enabled = false;
        if (sphereVertical != null) sphereVertical.enabled = false;
        if (sphereDepth != null) sphereDepth.enabled = false;
        if (xAxis != null) xAxis.enabled = false;
        if (yAxis != null) yAxis.enabled = false;
        if (zAxis != null) zAxis.enabled = false;
        if (rotationRing != null) rotationRing.enabled = false;
    }


    // ============================================================
    // PUBLIC API
    // ============================================================

    public GameObject GetSelectedObject()
    {
        return selectedObject;
    }

    public bool HasSelectedObject()
    {
        return selectedObject != null;
    }

    public void SetMoveMode()
    {
        currentMode = InteractionMode.Move;
    }

    public void SetRotateMode()
    {
        currentMode = InteractionMode.Rotate;
    }

    public void SetScaleMode()
    {
        currentMode = InteractionMode.Scale;
    }

    public void SetPlacing(bool value)
    {
        isPlacing = value;

        if (value)
        {
            ResetInputState();

            DeselectObject();
        }
    }

    public void SetGizmoVisible(bool visible)
    {
        showGizmo = visible;

        if (!visible)
            HideGizmo();
    }

    public void SetGizmoScale(float value)
    {
        gizmoScale = Mathf.Max(0.01f, value);

        if (selectedObject != null)
            UpdateGizmo();
    }

    public void SetGizmoShape(GizmoShape shape)
    {
        gizmoShape = shape;

        if (selectedObject != null)
            UpdateGizmo();
    }


    // ============================================================
    // CLEANUP
    // ============================================================

    private void OnDestroy()
    {
        DestroyMaterial(boxMaterial);
        DestroyMaterial(sphereMaterial);
        DestroyMaterial(xAxisMaterial);
        DestroyMaterial(yAxisMaterial);
        DestroyMaterial(zAxisMaterial);
        DestroyMaterial(rotationRingMaterial);

        if (gizmoRoot != null)
            Destroy(gizmoRoot);
    }

    private void DestroyMaterial(Material material)
    {
        if (material != null)
            Destroy(material);
    }
}
