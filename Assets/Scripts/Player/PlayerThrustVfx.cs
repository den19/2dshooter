using UnityEngine;

/// <summary>
/// Drives main (rear) and reverse (nose) thrust sprite animations from <see cref="Controller.VerticalMoveIntent"/>.
/// </summary>
public class PlayerThrustVfx : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Controller controller;
    [SerializeField] private SpriteRenderer mainThrustRenderer;
    [SerializeField] private SpriteRenderer reverseLeftRenderer;
    [SerializeField] private SpriteRenderer reverseRightRenderer;

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
    }

    private void LateUpdate()
    {
        if (controller == null || thrustSprites == null || thrustSprites.Length == 0)
        {
            return;
        }

        float intent = controller.VerticalMoveIntent;
        UpdateThrustState(intent);
        AnimateThrustSprites();
    }

    private void UpdateThrustState(float intent)
    {
        if (intent > activateThreshold)
        {
            mainThrustActive = true;
            reverseThrustActive = false;
        }
        else if (intent < -activateThreshold)
        {
            mainThrustActive = false;
            reverseThrustActive = true;
        }
        else
        {
            if (mainThrustActive && intent < deactivateThreshold)
            {
                mainThrustActive = false;
            }

            if (reverseThrustActive && intent > -deactivateThreshold)
            {
                reverseThrustActive = false;
            }
        }

        SetRendererActive(mainThrustRenderer, mainThrustActive);
        SetRendererActive(reverseLeftRenderer, reverseThrustActive);
        SetRendererActive(reverseRightRenderer, reverseThrustActive);

        if (!mainThrustActive && !reverseThrustActive)
        {
            frameTimer = 0f;
            frameIndex = 0;
        }
    }

    private void AnimateThrustSprites()
    {
        if (!mainThrustActive && !reverseThrustActive)
        {
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
