using UnityEngine;

/// <summary>
/// Drives player thrust sprite animations from <see cref="Controller.VerticalMoveIntent"/>
/// and <see cref="Controller.HorizontalMoveIntent"/>.
/// </summary>
public class PlayerThrustVfx : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Controller controller;
    [SerializeField] private SpriteRenderer mainThrustRenderer;
    [SerializeField] private SpriteRenderer reverseLeftRenderer;
    [SerializeField] private SpriteRenderer reverseRightRenderer;
    [SerializeField] private SpriteRenderer rightUpperThrustRenderer;
    [SerializeField] private SpriteRenderer rightLowerThrustRenderer;
    [SerializeField] private SpriteRenderer leftUpperThrustRenderer;
    [SerializeField] private SpriteRenderer leftLowerThrustRenderer;

    [Header("Sprites")]
    [Tooltip("Thrust_Start, Thrust_Accelerate, then loop frames from PlayerThrust.png")]
    [SerializeField] private Sprite[] thrustSprites;

    [Header("Tuning")]
    [SerializeField] private float activateThreshold = 0.15f;
    [SerializeField] private float deactivateThreshold = 0.08f;
    [SerializeField] private float animationFps = 12f;
    [SerializeField] private int loopStartFrameIndex = 2;
    [Tooltip("How long the last active thrusters remain visible after movement stops.")]
    [SerializeField] private float burnOutDuration = 1.5f;

    [Header("Visual Juice")]
    [Tooltip("Enable randomized high-frequency scale and alpha flickering to make the flames look hot and live.")]
    [SerializeField] private bool enableFlicker = true;
    [SerializeField] private float flickerScaleRangeX = 0.08f;
    [SerializeField] private float flickerScaleRangeY = 0.15f;
    [SerializeField] private float flickerAlphaRange = 0.15f;

    private bool mainThrustActive;
    private bool reverseThrustActive;
    private bool rightUpperThrustActive;
    private bool rightLowerThrustActive;
    private bool leftUpperThrustActive;
    private bool leftLowerThrustActive;

    private bool lastMainThrustActive;
    private bool lastReverseThrustActive;
    private bool lastRightUpperThrustActive;
    private bool lastRightLowerThrustActive;
    private bool lastLeftUpperThrustActive;
    private bool lastLeftLowerThrustActive;

    private Vector3 mainThrustBaseScale;
    private Vector3 reverseLeftBaseScale;
    private Vector3 reverseRightBaseScale;
    private Vector3 rightUpperBaseScale;
    private Vector3 rightLowerBaseScale;
    private Vector3 leftUpperBaseScale;
    private Vector3 leftLowerBaseScale;

    private float burnOutTimer;
    private float frameTimer;
    private int frameIndex;

    private void Awake()
    {
        if (controller == null)
        {
            controller = GetComponent<Controller>();
        }

        if (reverseLeftRenderer != null)
        {
            reverseLeftRenderer.flipY = true;
        }

        if (reverseRightRenderer != null)
        {
            reverseRightRenderer.flipY = true;
        }

        // Store original local scale values so we can flicker around them perfectly
        if (mainThrustRenderer != null) mainThrustBaseScale = mainThrustRenderer.transform.localScale;
        if (reverseLeftRenderer != null) reverseLeftBaseScale = reverseLeftRenderer.transform.localScale;
        if (reverseRightRenderer != null) reverseRightBaseScale = reverseRightRenderer.transform.localScale;
        if (rightUpperThrustRenderer != null) rightUpperBaseScale = rightUpperThrustRenderer.transform.localScale;
        if (rightLowerThrustRenderer != null) rightLowerBaseScale = rightLowerThrustRenderer.transform.localScale;
        if (leftUpperThrustRenderer != null) leftUpperBaseScale = leftUpperThrustRenderer.transform.localScale;
        if (leftLowerThrustRenderer != null) leftLowerBaseScale = leftLowerThrustRenderer.transform.localScale;

        SetRendererActive(mainThrustRenderer, false);
        SetRendererActive(reverseLeftRenderer, false);
        SetRendererActive(reverseRightRenderer, false);
        SetRendererActive(rightUpperThrustRenderer, false);
        SetRendererActive(rightLowerThrustRenderer, false);
        SetRendererActive(leftUpperThrustRenderer, false);
        SetRendererActive(leftLowerThrustRenderer, false);
    }

    private void LateUpdate()
    {
        if (controller == null || thrustSprites == null || thrustSprites.Length == 0)
        {
            return;
        }

        bool isMovingActive = Mathf.Abs(controller.VerticalMoveIntent) >= deactivateThreshold || 
                             Mathf.Abs(controller.HorizontalMoveIntent) >= deactivateThreshold;

        if (isMovingActive)
        {
            burnOutTimer = burnOutDuration;

            UpdateVerticalThrustState(controller.VerticalMoveIntent);
            UpdateRightThrustState(controller.HorizontalMoveIntent, controller.VerticalMoveIntent);
            UpdateLeftThrustState(controller.HorizontalMoveIntent, controller.VerticalMoveIntent);

            SaveLastActiveStates();
        }
        else
        {
            if (burnOutTimer > 0f)
            {
                burnOutTimer -= Time.deltaTime;
                ApplyLastActiveStates();
            }
            else
            {
                ClearAllThrustStates();
            }
        }

        AnimateThrustSprites();
        ApplyVisualEffects();
    }

    private void SaveLastActiveStates()
    {
        if (mainThrustActive || reverseThrustActive || 
            rightUpperThrustActive || rightLowerThrustActive || 
            leftUpperThrustActive || leftLowerThrustActive)
        {
            lastMainThrustActive = mainThrustActive;
            lastReverseThrustActive = reverseThrustActive;
            lastRightUpperThrustActive = rightUpperThrustActive;
            lastRightLowerThrustActive = rightLowerThrustActive;
            lastLeftUpperThrustActive = leftUpperThrustActive;
            lastLeftLowerThrustActive = leftLowerThrustActive;
        }
    }

    private void ApplyLastActiveStates()
    {
        mainThrustActive = lastMainThrustActive;
        reverseThrustActive = lastReverseThrustActive;
        rightUpperThrustActive = lastRightUpperThrustActive;
        rightLowerThrustActive = lastRightLowerThrustActive;
        leftUpperThrustActive = lastLeftUpperThrustActive;
        leftLowerThrustActive = lastLeftLowerThrustActive;

        SetRendererActive(mainThrustRenderer, mainThrustActive);
        SetRendererActive(reverseLeftRenderer, reverseThrustActive);
        SetRendererActive(reverseRightRenderer, reverseThrustActive);
        SetRendererActive(rightUpperThrustRenderer, rightUpperThrustActive);
        SetRendererActive(rightLowerThrustRenderer, rightLowerThrustActive);
        SetRendererActive(leftUpperThrustRenderer, leftUpperThrustActive);
        SetRendererActive(leftLowerThrustRenderer, leftLowerThrustActive);
    }

    private void ClearAllThrustStates()
    {
        mainThrustActive = false;
        reverseThrustActive = false;
        rightUpperThrustActive = false;
        rightLowerThrustActive = false;
        leftUpperThrustActive = false;
        leftLowerThrustActive = false;

        SetRendererActive(mainThrustRenderer, false);
        SetRendererActive(reverseLeftRenderer, false);
        SetRendererActive(reverseRightRenderer, false);
        SetRendererActive(rightUpperThrustRenderer, false);
        SetRendererActive(rightLowerThrustRenderer, false);
        SetRendererActive(leftUpperThrustRenderer, false);
        SetRendererActive(leftLowerThrustRenderer, false);

        ResetRendererVisuals(mainThrustRenderer, mainThrustBaseScale);
        ResetRendererVisuals(reverseLeftRenderer, reverseLeftBaseScale);
        ResetRendererVisuals(reverseRightRenderer, reverseRightBaseScale);
        ResetRendererVisuals(rightUpperThrustRenderer, rightUpperBaseScale);
        ResetRendererVisuals(rightLowerThrustRenderer, rightLowerBaseScale);
        ResetRendererVisuals(leftUpperThrustRenderer, leftUpperBaseScale);
        ResetRendererVisuals(leftLowerThrustRenderer, leftLowerBaseScale);

        lastMainThrustActive = false;
        lastReverseThrustActive = false;
        lastRightUpperThrustActive = false;
        lastRightLowerThrustActive = false;
        lastLeftUpperThrustActive = false;
        lastLeftLowerThrustActive = false;
    }

    private void ResetRendererVisuals(SpriteRenderer renderer, Vector3 baseScale)
    {
        if (renderer == null) return;
        renderer.transform.localScale = baseScale;
        Color c = renderer.color;
        c.a = 1f;
        renderer.color = c;
    }

    private void ApplyVisualEffects()
    {
        bool isMovingActive = Mathf.Abs(controller.VerticalMoveIntent) >= deactivateThreshold || 
                             Mathf.Abs(controller.HorizontalMoveIntent) >= deactivateThreshold;

        // Base alpha is 1 when moving, and fades out beautifully during burnout duration
        float baseAlpha = isMovingActive ? 1.0f : Mathf.Clamp01(burnOutTimer / burnOutDuration);

        ApplyFlickerAndAlpha(mainThrustRenderer, mainThrustBaseScale, baseAlpha, mainThrustActive);
        ApplyFlickerAndAlpha(reverseLeftRenderer, reverseLeftBaseScale, baseAlpha, reverseThrustActive);
        ApplyFlickerAndAlpha(reverseRightRenderer, reverseRightBaseScale, baseAlpha, reverseThrustActive);
        ApplyFlickerAndAlpha(rightUpperThrustRenderer, rightUpperBaseScale, baseAlpha, rightUpperThrustActive);
        ApplyFlickerAndAlpha(rightLowerThrustRenderer, rightLowerBaseScale, baseAlpha, rightLowerThrustActive);
        ApplyFlickerAndAlpha(leftUpperThrustRenderer, leftUpperBaseScale, baseAlpha, leftUpperThrustActive);
        ApplyFlickerAndAlpha(leftLowerThrustRenderer, leftLowerBaseScale, baseAlpha, leftLowerThrustActive);
    }

    private void ApplyFlickerAndAlpha(SpriteRenderer renderer, Vector3 baseScale, float baseAlpha, bool isActive)
    {
        if (renderer == null) return;
        if (!renderer.enabled || !isActive) return;

        // High-frequency randomized scale fluctuation to look like turbulent plasma
        float scaleModX = enableFlicker ? Random.Range(1f - flickerScaleRangeX, 1f + flickerScaleRangeX) : 1f;
        float scaleModY = enableFlicker ? Random.Range(1f - flickerScaleRangeY, 1f + flickerScaleRangeY) : 1f;

        renderer.transform.localScale = new Vector3(baseScale.x * scaleModX, baseScale.y * scaleModY, baseScale.z);

        // Alpha fade out + micro-flicker
        float finalAlpha = baseAlpha;
        if (enableFlicker)
        {
            finalAlpha *= Random.Range(1f - flickerAlphaRange, 1f);
        }

        Color c = renderer.color;
        c.a = Mathf.Clamp01(finalAlpha);
        renderer.color = c;
    }

    private void UpdateVerticalThrustState(float verticalIntent)
    {
        if (verticalIntent > activateThreshold)
        {
            mainThrustActive = true;
            reverseThrustActive = false;
        }
        else if (verticalIntent < -activateThreshold)
        {
            mainThrustActive = false;
            reverseThrustActive = true;
        }
        else
        {
            if (mainThrustActive && verticalIntent < deactivateThreshold)
            {
                mainThrustActive = false;
            }

            if (reverseThrustActive && verticalIntent > -deactivateThreshold)
            {
                reverseThrustActive = false;
            }
        }

        SetRendererActive(mainThrustRenderer, mainThrustActive);
        SetRendererActive(reverseLeftRenderer, reverseThrustActive);
        SetRendererActive(reverseRightRenderer, reverseThrustActive);
    }

    private void UpdateRightThrustState(float horizontalIntent, float verticalIntent)
    {
        if (horizontalIntent < -activateThreshold)
        {
            if (verticalIntent > activateThreshold)
            {
                rightUpperThrustActive = false;
                rightLowerThrustActive = true;
            }
            else if (verticalIntent < -activateThreshold)
            {
                rightUpperThrustActive = true;
                rightLowerThrustActive = false;
            }
            else
            {
                rightUpperThrustActive = true;
                rightLowerThrustActive = true;
            }
        }
        else if (horizontalIntent > -deactivateThreshold)
        {
            rightUpperThrustActive = false;
            rightLowerThrustActive = false;
        }
        else
        {
            if (rightUpperThrustActive && horizontalIntent > -deactivateThreshold)
            {
                rightUpperThrustActive = false;
            }

            if (rightLowerThrustActive && horizontalIntent > -deactivateThreshold)
            {
                rightLowerThrustActive = false;
            }
        }

        SetRendererActive(rightUpperThrustRenderer, rightUpperThrustActive);
        SetRendererActive(rightLowerThrustRenderer, rightLowerThrustActive);
    }

    private void UpdateLeftThrustState(float horizontalIntent, float verticalIntent)
    {
        if (horizontalIntent > activateThreshold)
        {
            if (verticalIntent > activateThreshold)
            {
                leftUpperThrustActive = false;
                leftLowerThrustActive = true;
            }
            else if (verticalIntent < -activateThreshold)
            {
                leftUpperThrustActive = true;
                leftLowerThrustActive = false;
            }
            else
            {
                leftUpperThrustActive = true;
                leftLowerThrustActive = true;
            }
        }
        else if (horizontalIntent < deactivateThreshold)
        {
            leftUpperThrustActive = false;
            leftLowerThrustActive = false;
        }
        else
        {
            if (leftUpperThrustActive && horizontalIntent < deactivateThreshold)
            {
                leftUpperThrustActive = false;
            }

            if (leftLowerThrustActive && horizontalIntent < deactivateThreshold)
            {
                leftLowerThrustActive = false;
            }
        }

        SetRendererActive(leftUpperThrustRenderer, leftUpperThrustActive);
        SetRendererActive(leftLowerThrustRenderer, leftLowerThrustActive);
    }

    private void AnimateThrustSprites()
    {
        bool anyActive = mainThrustActive || reverseThrustActive
            || rightUpperThrustActive || rightLowerThrustActive
            || leftUpperThrustActive || leftLowerThrustActive;

        if (!anyActive)
        {
            frameTimer = 0f;
            frameIndex = 0;
            return;
        }

        frameTimer += Time.deltaTime;
        float frameDuration = 1f / Mathf.Max(animationFps, 1f);

        while (frameTimer >= frameDuration)
        {
            frameTimer -= frameDuration;
            frameIndex++;

            int loopStart = Mathf.Clamp(loopStartFrameIndex, 0, thrustSprites.Length - 1);
            int loopLength = thrustSprites.Length - loopStart;

            if (frameIndex < loopStart)
            {
                continue;
            }

            if (loopLength <= 1)
            {
                frameIndex = loopStart;
                continue;
            }

            frameIndex = loopStart + ((frameIndex - loopStart) % loopLength);
        }

        int spriteIndex = Mathf.Clamp(frameIndex, 0, thrustSprites.Length - 1);
        Sprite frame = thrustSprites[spriteIndex];

        if (mainThrustActive && mainThrustRenderer != null)
        {
            mainThrustRenderer.sprite = frame;
        }

        if (reverseThrustActive)
        {
            if (reverseLeftRenderer != null)
            {
                reverseLeftRenderer.sprite = frame;
            }

            if (reverseRightRenderer != null)
            {
                reverseRightRenderer.sprite = frame;
            }
        }

        if (rightUpperThrustActive && rightUpperThrustRenderer != null)
        {
            rightUpperThrustRenderer.sprite = frame;
        }

        if (rightLowerThrustActive && rightLowerThrustRenderer != null)
        {
            rightLowerThrustRenderer.sprite = frame;
        }

        if (leftUpperThrustActive && leftUpperThrustRenderer != null)
        {
            leftUpperThrustRenderer.sprite = frame;
        }

        if (leftLowerThrustActive && leftLowerThrustRenderer != null)
        {
            leftLowerThrustRenderer.sprite = frame;
        }
    }

    private static void SetRendererActive(SpriteRenderer renderer, bool active)
    {
        if (renderer == null)
        {
            return;
        }

        renderer.enabled = active;
    }
}
