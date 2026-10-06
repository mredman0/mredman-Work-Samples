using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("Required References")]
    public GameObject CameraArm;

    [Header("Optional References")]
    public Puzzle Puzzle;
    
    [Header("Generic Settings")]
    public float Speed = 8f;
    public float MaxSpeed = 1f;
    public float Friction = 1f;
    public float ControlModeFriction = 10f;
    public float RotationSensitivity = 1f;
    public bool AllowMomentumWithActiveNode = false;
    public int InputLocks = 0;
    public bool DoPuzzleCompleteSpin;
#if ENABLE_MARKETING_TOOLSET || UNITY_EDITOR
    public bool DoMarketingSpin = false;
    public float MarketingSpinStartTime;
#endif

    [Header("Compatibility Settings")]
    // Represents the W/H of the most "square" aspect ratio before the puzzle would appear too big
    public float MaxAspectRatio = 0.58514f;

    private int DragInputsToStore = 5;

    [Header("Locked Roll Settings")]
    public bool AllowRoll = true;
    public float PitchInvalidLow = 5f;
    public float PitchInvalidHigh = 5f;

    [Header("Gradual Snap Settings")]
    public float GradualSnapDegreesPerSecond = 500f;

    [Header("Gradual Snap State")]
    public float GradualSnapDuration;
    public float GradualSnapTime;
    public Quaternion GradualSnapStart;
    public Quaternion GradualSnapEnd;
    public bool GradualSnapLockUntilRelease = false;

    public bool ControlMode = false;
    public bool Panning { get; set; } = false;
    public float PanAmountThisDrag { get; set; } = 0f;
    public Camera Camera { get; set; }

    private List<Vector2> LastDragMotions = new List<Vector2>();
    private Vector2 Momentum;
    private float RotateMomentum;

    private bool InvertFreeLook;
    private bool InvertDrawing;
    private float Sensitivity;

    private float FixedRollSnap = 0f;

    // Start is called before the first frame update
    void Start()
    {
        Camera = GetComponent<Camera>();

        SettingsManager.Instance.BoolSettingChanged += OnBoolSettingChanged;
        SettingsManager.Instance.FloatSettingChanged += OnFloatSettingChanged;

        InputManager.Instance.Press += OnPress;
        InputManager.Instance.Release += OnRelease;
        InputManager.Instance.Drag += OnDrag;
        InputManager.Instance.Rotate += OnRotate;

        if (Puzzle)
        {
            Puzzle.NodeSelected += OnNodeSelected;
            Puzzle.NodeDeselected += OnNodeDeselected;
            Puzzle.ResetUsed += OnNodeDeselected;

            Puzzle.PuzzleCompleted += OnPuzzleCompleted;
        }

        AllowRoll = SettingsManager.Instance.GetBool(ALLOW_ROLL_KEY);
        InvertFreeLook = SettingsManager.Instance.GetBool(INVERT_FREE_LOOK_KEY);
        InvertDrawing = SettingsManager.Instance.GetBool(INVERT_DRAWING_KEY);
        Sensitivity = SettingsManager.Instance.GetFloat(SENSITIVITY_KEY);
    }

    private void OnDestroy()
    {
        SettingsManager.Instance.BoolSettingChanged -= OnBoolSettingChanged;
        SettingsManager.Instance.FloatSettingChanged -= OnFloatSettingChanged;

        InputManager.Instance.Press -= OnPress;
        InputManager.Instance.Release -= OnRelease;
        InputManager.Instance.Drag -= OnDrag;
        InputManager.Instance.Rotate -= OnRotate;

        if (Puzzle)
        {
            Puzzle.PuzzleCompleted -= OnPuzzleCompleted;
        }
    }

    private const string ALLOW_ROLL_KEY = "ControlsAllowRoll";
    private const string INVERT_FREE_LOOK_KEY = "ControlsInvertFreeLook";
    private const string INVERT_DRAWING_KEY = "ControlsInvertDrawing";
    private void OnBoolSettingChanged(string setting, bool value)
    {
        if(setting == ALLOW_ROLL_KEY)
        {
            AllowRoll = value;
            if(!AllowRoll)
            {
                FixRoll();
            }
        }
        else if(setting == INVERT_FREE_LOOK_KEY)
        {
            InvertFreeLook = value;
        }
        else if(setting == INVERT_DRAWING_KEY)
        {
            InvertDrawing = value;
        }
    }

    private const string SENSITIVITY_KEY = "ControlsSensitivity";
    private void OnFloatSettingChanged(string setting, float value)
    {
        if(setting == SENSITIVITY_KEY)
        {
            Sensitivity = value;
        }
    }

    private void OnPress(Vector2 position)
    {
        if(InputLocks > 0)
        {
            return;
        }
        DoPuzzleCompleteSpin = false;
        Momentum = Vector2.zero;
        RotateMomentum = 0;
    }

    private void OnRelease(Vector2 position)
    {
        if (GradualSnapLockUntilRelease)
        {
            GradualSnapLockUntilRelease = false;
            FreeInput();
            Puzzle.FreeInput();
            LastDragMotions.Clear();
        }
        if (InputLocks > 0)
        {
            return;
        }
        DoPuzzleCompleteSpin = false;
        var avgOfLatestDrags = Vector2.zero;
        foreach(var drag in LastDragMotions)
        {
            avgOfLatestDrags += drag;
        }
        avgOfLatestDrags /= DragInputsToStore;
        Momentum = avgOfLatestDrags;
        //if (Momentum.magnitude > MaxSpeed)
        //{
        //    Momentum *= MaxSpeed / Momentum.magnitude;
        //}
        LastDragMotions.Clear();
        Panning = false;
        PanAmountThisDrag = 0f;
    }

    private void OnDrag(Vector2 drag)
    {
        if (InputLocks > 0)
        {
            return;
        }
        var normalizedDrag = InputManager.Instance.NormalizeScreenPosition(drag);
        DoPuzzleCompleteSpin = false;
        Panning = true;
        HandleDrag(normalizedDrag);
    }

    private void OnRotate(float rotateAmount)
    {
        if (InputLocks > 0)
        {
            return;
        }
        DoPuzzleCompleteSpin = false;
        Panning = true;
        var rotationAdjusted = rotateAmount * -1f * RotationSensitivity;
        CameraArm.transform.Rotate(0, 0, rotationAdjusted, Space.Self);
        RotateMomentum = rotationAdjusted;
    }

    private void OnNodeSelected(Node node)
    {
        ControlMode = true;
    }

    private void OnNodeDeselected()
    {
        ControlMode = false;
    }

    private void OnPuzzleCompleted()
    {
#if ENABLE_MARKETING_TOOLSET || UNITY_EDITOR
        DoPuzzleCompleteSpin = !DoMarketingSpin;
#else
        DoPuzzleCompleteSpin = true;
#endif
        Panning = false;
    }

    private void HandleDrag(Vector2 drag)
    {
        var motion = drag * Sensitivity * Speed;
        if (motion.magnitude > MaxSpeed)
        {
            motion *= MaxSpeed / motion.magnitude;
        }

        if(Puzzle.ActiveNode && InvertDrawing || !Puzzle.ActiveNode && InvertFreeLook)
        {
            motion *= -1f;
        }

        PanAmountThisDrag += motion.magnitude;

        LastDragMotions.Add(motion);
        if (LastDragMotions.Count > DragInputsToStore)
        {
            LastDragMotions.RemoveAt(0);
        }
        HandleMotion(motion);
    }

    private void HandleMotion(Vector2 motion)
    {
        if (!AllowRoll)
        {
            // not sure why I have to do this
            var pitch = CameraArm.transform.localEulerAngles.x;
            motion.x *= Mathf.Cos(Mathf.Deg2Rad * pitch);
        }

        CameraArm.transform.Rotate(Vector3.up, motion.x);
        CameraArm.transform.Rotate(Vector3.right, motion.y);

        var lastTryMotion = new Vector2(motion.x, motion.y);
        if (Puzzle)
        {
            var tryNum = 0;
            var maxTries = 6;
            var rotateEveryOtherTry = 18f;
            var currentRotate = 0f;
            while (!Puzzle.IsCameraPositionValid())
            {
                // Revert previous try
                CameraArm.transform.Rotate(Vector3.right, lastTryMotion.y * -1);
                CameraArm.transform.Rotate(Vector3.up, lastTryMotion.x * -1);

                if (tryNum > maxTries)
                {
                    break;
                }

                // Try new angle
                var rotationDirection = Mathf.Sign((tryNum % 2) - 0.5f);
                if (rotationDirection > 0)
                {
                    currentRotate += rotateEveryOtherTry;
                }
                lastTryMotion = Quaternion.Euler(0, 0, currentRotate * rotationDirection) * new Vector3(motion.x, motion.y, 0);
                CameraArm.transform.Rotate(Vector3.up, lastTryMotion.x);
                CameraArm.transform.Rotate(Vector3.right, lastTryMotion.y);

                tryNum++;
            }
        }

        if (!AllowRoll && !DoPuzzleCompleteSpin)
        {
            FixRoll();
        }
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        if(GradualSnapDuration > 0)
        {
            Momentum = Vector3.zero;
            RotateMomentum = 0;
            GradualSnapTime += Time.fixedDeltaTime;

            var t = Mathf.Clamp01(GradualSnapTime / GradualSnapDuration);
            CameraArm.transform.rotation = Quaternion.Lerp(GradualSnapStart, GradualSnapEnd, t);
            if (!AllowRoll)
            {
                FixRoll();
            }

            if (GradualSnapTime >= GradualSnapDuration)
            {
                GradualSnapComplete();
            }
        }

        if(DoPuzzleCompleteSpin)
        {
            Momentum = Vector2.right * Mathf.Sin(Time.time + Mathf.PI/2f) + Vector2.up * Mathf.Cos(Time.time/2f);
        }
#if ENABLE_MARKETING_TOOLSET || UNITY_EDITOR
        if(DoMarketingSpin)
        {
            var speed = 0.45f;
            float timePerRotation = 360f / speed * Time.fixedDeltaTime; // Total time for 360 degrees
            Momentum = Vector2.right * speed + Vector2.up * Mathf.Cos((Time.time - MarketingSpinStartTime) * 2f * Mathf.PI / timePerRotation) / 5f;
        }
#endif
        if (!Panning)
        {
            if ((AllowMomentumWithActiveNode || !Puzzle || !Puzzle.ActiveNode) && (Momentum.x != 0 || Momentum.y != 0))
            {
                HandleMotion(Momentum);
                var friction = ControlMode ? ControlModeFriction : Friction;
                Momentum *= (1 - Mathf.Clamp(friction * Time.fixedDeltaTime, 0, 1));
            }
            if ((AllowMomentumWithActiveNode || !Puzzle || !Puzzle.ActiveNode) && (RotateMomentum != 0))
            {
                CameraArm.transform.Rotate(0, 0, RotateMomentum, Space.Self);
                var friction = ControlMode ? ControlModeFriction : Friction;
                RotateMomentum *= (1 - Mathf.Clamp(friction * Time.fixedDeltaTime, 0, 1));
            }
        }
    }

    public void LockInput() => InputLocks++;
    public void FreeInput() => InputLocks = Mathf.Max(InputLocks - 1, 0);

    public void SnapToNodeEndOfPath(Node n)
    {
        if(!n.Path)
        {
            Debug.LogError($"{n} does not have a line to snap the camera to the end of!");
            return;
        }
        CameraArm.transform.LookAt(n.Path.GetPosition(n.Path.PositionCount - 1), CameraArm.transform.up);
        if(!AllowRoll)
        {
            FixRoll();
        }
    }

    public void SnapToGridCell(GridCell cell)
    {
        CameraArm.transform.LookAt(cell.transform.position, CameraArm.transform.up);
        if (!AllowRoll)
        {
            FixRoll();
        }
    }

    public void SnapTo(Quaternion armRotation, float cameraDistance, float cameraFoV)
    {
        CameraArm.transform.rotation = armRotation;
        if(!AllowRoll)
        {
            FixRoll();
        }

        if(cameraDistance <= 0)
        {
            // Safe value
            cameraDistance = 5.73f;
        }
        if(cameraFoV <= 0)
        {
            // Safe value
            cameraFoV = 22.2f;
        }

        var aspectRatio = (float)Screen.width / Screen.height;
        if(aspectRatio > MaxAspectRatio)
        {
            var adjustment = (aspectRatio / MaxAspectRatio - 1) / -3f;
            var cameraRect = Camera.rect;
            cameraRect.yMin = adjustment;
            cameraRect.size = new Vector2(1, 1);
            Camera.rect = cameraRect;
            Debug.Log($"Shifted camera render down by {adjustment}");
        }

        Camera.transform.localPosition = Vector3.forward * cameraDistance;
        var fovFixer = GetComponent<MaintainHorizontalFOV>();
        if(fovFixer)
        {
            fovFixer.desiredHorizontalFOV = cameraFoV;
            fovFixer.UpdateCameraFOV();
        }
        else
        {
            Camera.fieldOfView = cameraFoV;
        }
    }

    public void GradualSnapToGridCell(GridCell cell) => GradualSnapToLookAt(cell.transform.position);

    public void GradualSnapToLookAt(Vector3 lookAt)
    {
        var end = Quaternion.LookRotation(lookAt - CameraArm.transform.position, CameraArm.transform.up);
        GradualSnap(CameraArm.transform.rotation, end);
    }

    public void GradualSnap(Quaternion end) => GradualSnap(CameraArm.transform.rotation, end);

    public void GradualSnap(Quaternion start, Quaternion end)
    {
        if(!AllowRoll)
        {
            var endEuler = end.eulerAngles;
            end = Quaternion.Euler(endEuler.x, endEuler.y, start.eulerAngles.z);
        }
        var degreesBetween = Quaternion.Angle(start, end);
        if(degreesBetween == 0)
        {
            return;
        }
        if (GradualSnapDuration == 0)
        {
            LockInput();
            Puzzle.LockInput();
        }

        GradualSnapLockUntilRelease = true;
        if (GradualSnapDuration == 0)
        {
            LockInput();
            Puzzle.LockInput();
        }

        GradualSnapStart = start;
        GradualSnapEnd = end;
        GradualSnapDuration = degreesBetween / GradualSnapDegreesPerSecond;
        GradualSnapTime = 0f;


    }

    private void GradualSnapComplete()
    {
        FreeInput();
        Puzzle.FreeInput();
        GradualSnapDuration = 0f;

        if(!Panning)
        {
            GradualSnapLockUntilRelease = false;
            FreeInput();
            Puzzle.FreeInput();
        }
    }

    private void FixRoll()
    {
        if(InputManager.Instance.TwoTouching)
        {
            return;
        }

        var prevSnap = FixedRollSnap;
        FixedRollSnap = Mathf.Abs(180f - CameraArm.transform.localEulerAngles.z) < 90f ? 180f : 0f;
        if (FixedRollSnap != prevSnap)
        {
            Momentum.x *= -1f;
        }

        var rot = CameraArm.transform.localEulerAngles;
        rot.z = FixedRollSnap;
        CameraArm.transform.rotation = Quaternion.Euler(rot);
    }

    private static float InverseClamp(float value, float lowBound, float highBound)
    {
        if (value <= lowBound || value >= highBound)
        {
            return value;
        }
        if (Mathf.Abs(value - lowBound) < Mathf.Abs(value - highBound))
        {
            return lowBound;
        }
        return highBound;
    }
}
