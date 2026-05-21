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

    private bool mainThrustActive;
    private bool reverseThrustActive;
    private bool rightUpperThrustActive;
    private bool rightLowerThrustActive;
    private bool leftUpperThrustActive;
    private bool leftLowerThrustActive;
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

        UpdateVerticalThrustState(controller.VerticalMoveIntent);
        UpdateRightThrustState(controller.HorizontalMoveIntent, controller.VerticalMoveIntent);
        UpdateLeftThrustState(controller.HorizontalMoveIntent, controller.VerticalMoveIntent);
        AnimateThrustSprites();
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
