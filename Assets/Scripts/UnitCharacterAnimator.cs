using UnityEngine;

/// <summary>
/// Drives a rigged character from its <see cref="Unit"/>'s motion: blends
/// idle → walk → run by ground speed so the feet keep pace with the ground,
/// speeds the run cycle up beyond its natural pace, and leans the body into
/// turns. Only touches the visual model, never the logical unit.
/// </summary>
public class UnitCharacterAnimator : MonoBehaviour
{
    public Unit unit;
    public Animator animator;

    [Tooltip("Ground speed (world units/sec) at which the walk cycle's feet don't slide.")]
    public float walkCycleSpeed = 0.9f;

    [Tooltip("Ground speed (world units/sec) at which the run cycle's feet don't slide.")]
    public float runCycleSpeed = 1.8f;

    [Tooltip("Degrees of sideways lean per 100°/sec of turning.")]
    public float leanPerTurnRate = 6f;

    [Tooltip("Maximum sideways lean in degrees.")]
    public float maxLean = 8f;

    static readonly int SpeedParam = Animator.StringToHash("Speed");

    float lean;
    float lastYaw;

    void Start()
    {
        if (unit == null) unit = GetComponentInParent<Unit>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        lastYaw = unit != null ? unit.transform.eulerAngles.y : 0f;

        // Desynchronise idles so a group of units doesn't breathe in unison.
        if (animator != null && animator.runtimeAnimatorController != null)
            animator.Play(0, 0, Random.value);
    }

    void Update()
    {
        if (unit == null || animator == null) return;
        float dt = Time.deltaTime;
        float speed = unit.CurrentSpeed;

        animator.SetFloat(SpeedParam, speed, 0.05f, dt);
        animator.speed = speed > runCycleSpeed ? speed / runCycleSpeed : 1f;

        float yaw = unit.transform.eulerAngles.y;
        float yawRate = dt > 0f ? Mathf.DeltaAngle(lastYaw, yaw) / dt : 0f;
        lastYaw = yaw;
        // Lean scales with speed: a unit pivoting on the spot stays upright.
        float wantLean = Mathf.Clamp(-yawRate / 100f * leanPerTurnRate, -maxLean, maxLean)
                         * Mathf.Clamp01(speed / runCycleSpeed);
        lean = Mathf.Lerp(lean, wantLean, 1f - Mathf.Exp(-8f * dt));
        transform.localRotation = Quaternion.Euler(0f, 0f, lean);
    }
}
