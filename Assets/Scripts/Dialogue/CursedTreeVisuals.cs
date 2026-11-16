using System;
using UnityEngine;

public class CursedTreeVisuals : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform player;
    [SerializeField] private TreeDialogueController dialogueController;
    [SerializeField] private string treeSpeakerName = "Alderworn";
    [SerializeField] private SpriteRenderer body;
    [SerializeField] private SpriteRenderer leftEye;
    [SerializeField] private SpriteRenderer rightEye;
    [SerializeField] private SpriteRenderer mouth;
    [Header("Body idle")]
    [SerializeField] private Sprite[] idleFrames;
    [SerializeField, Min(0.1f)] private float idleFramesPerSecond = 4f;
    [Header("Eye poses")]
    [SerializeField] private Sprite leftOpen;
    [SerializeField] private Sprite rightOpen;
    [SerializeField] private Sprite leftLookLeft;
    [SerializeField] private Sprite rightLookLeft;
    [SerializeField] private Sprite leftLookRight;
    [SerializeField] private Sprite rightLookRight;
    [SerializeField] private Sprite leftLookDown;
    [SerializeField] private Sprite rightLookDown;
    [SerializeField] private Sprite leftClosed;
    [SerializeField] private Sprite rightClosed;
    [SerializeField] private Vector2 blinkInterval = new Vector2(3f, 6f);
    [SerializeField, Min(0.02f)] private float blinkSeconds = 0.12f;
    [Header("Look at player")]
    [SerializeField] private Vector3 playerLookOffset = new Vector3(0f, 0.7f, 0f);
    [SerializeField, Min(0.1f)] private float lookRange = 14f;
    [SerializeField, Min(0.1f)] private float fullLookDistance = 3f;
    [SerializeField] private Vector2 maximumEyeOffset = new Vector2(0.015f, 0.01f);
    [SerializeField, Min(0.1f)] private float eyeFollowSpeed = 10f;
    [SerializeField] private bool snapEyesToPixels = true;
    [SerializeField, Min(1f)] private float pixelsPerUnit = 100f;
    [Header("Talking mouth")]
    [SerializeField] private Sprite restingMouth;
    [SerializeField] private Sprite[] talkingMouthFrames;
    [SerializeField] private Vector2 mouthFrameInterval = new Vector2(0.10f, 0.17f);
    private Vector3 leftHome, rightHome;
    private Vector2 eyeOffset;
    private float idleClock, nextBlink, blinkUntil, nextMouthFrame;
    private int mouthFrame;
    private System.Random random;
    private bool initialized;

    private void Awake()
    {
        if (!player || !body || !leftEye || !rightEye || !mouth || leftEye == rightEye ||
            leftEye == body || rightEye == body || mouth == body || mouth == leftEye || mouth == rightEye ||
            !leftEye.transform.parent || !rightEye.transform.parent)
        { Debug.LogError("CursedTreeVisuals needs scene Player and separate Body, LeftEye, RightEye and Mouth SpriteRenderers.", this); enabled = false; return; }
        leftHome = leftEye.transform.localPosition;
        rightHome = rightEye.transform.localPosition;
        random = new System.Random(GetEntityId().GetHashCode());
        nextBlink = Time.unscaledTime + RandomInterval(blinkInterval);
        leftEye.sortingLayerID = rightEye.sortingLayerID = mouth.sortingLayerID = body.sortingLayerID;
        leftEye.sortingOrder = rightEye.sortingOrder = mouth.sortingOrder = body.sortingOrder + 1;
        initialized = true;
    }

    private void LateUpdate()
    {
        if (!initialized) return;
        float now = Time.unscaledTime;
        idleClock += Time.unscaledDeltaTime;
        if (idleFrames != null && idleFrames.Length > 0)
        {
            Sprite frame = idleFrames[Mathf.FloorToInt(idleClock * Mathf.Max(0.1f, idleFramesPerSecond)) % idleFrames.Length];
            if (frame) body.sprite = frame;
        }
        Vector3 eyeCenter = (leftEye.transform.parent.TransformPoint(leftHome) +
            rightEye.transform.parent.TransformPoint(rightHome)) * 0.5f;
        Vector2 delta = player ? (Vector2)(player.position + playerLookOffset - eyeCenter) : Vector2.zero;
        bool looking = player && delta.sqrMagnitude <= lookRange * lookRange;
        Vector2 targetOffset = looking ? new Vector2(
            Mathf.Clamp(delta.x / Mathf.Max(0.1f, fullLookDistance), -1f, 1f) * Mathf.Abs(maximumEyeOffset.x),
            Mathf.Clamp(delta.y / Mathf.Max(0.1f, fullLookDistance), -1f, 1f) * Mathf.Abs(maximumEyeOffset.y)) : Vector2.zero;
        eyeOffset = Vector2.Lerp(eyeOffset, targetOffset, 1f - Mathf.Exp(-eyeFollowSpeed * Time.unscaledDeltaTime));
        Vector2 drawnOffset = eyeOffset;
        if (snapEyesToPixels)
        {
            float ppu = Mathf.Max(1f, pixelsPerUnit);
            drawnOffset.x = Mathf.Round(drawnOffset.x * ppu) / ppu;
            drawnOffset.y = Mathf.Round(drawnOffset.y * ppu) / ppu;
        }
        leftEye.transform.localPosition = leftHome + (Vector3)drawnOffset;
        rightEye.transform.localPosition = rightHome + (Vector3)drawnOffset;
        if (now >= nextBlink)
        {
            blinkUntil = now + Mathf.Max(0.02f, blinkSeconds);
            nextBlink = blinkUntil + RandomInterval(blinkInterval);
        }
        bool blinking = now < blinkUntil;
        Sprite left = leftOpen, right = rightOpen;
        if (looking && delta.x < -0.75f) { left = leftLookLeft ? leftLookLeft : left; right = rightLookLeft ? rightLookLeft : right; }
        else if (looking && delta.x > 0.75f) { left = leftLookRight ? leftLookRight : left; right = rightLookRight ? rightLookRight : right; }
        else if (looking && delta.y < -0.5f) { left = leftLookDown ? leftLookDown : left; right = rightLookDown ? rightLookDown : right; }
        if (blinking) { if (leftClosed) left = leftClosed; if (rightClosed) right = rightClosed; }
        if (left) leftEye.sprite = left;
        if (right) rightEye.sprite = right;
        bool talking = dialogueController && dialogueController.IsOpen &&
            string.Equals(dialogueController.CurrentSpeaker, treeSpeakerName, StringComparison.OrdinalIgnoreCase) &&
            (dialogueController.IsTyping || !dialogueController.HasTypewriter);
        if (talking && talkingMouthFrames != null && talkingMouthFrames.Length > 0)
        {
            if (now >= nextMouthFrame)
            {
                mouthFrame = (mouthFrame + 1) % talkingMouthFrames.Length;
                nextMouthFrame = now + RandomInterval(mouthFrameInterval);
            }
            if (talkingMouthFrames[mouthFrame]) mouth.sprite = talkingMouthFrames[mouthFrame];
        }
        else
        {
            mouthFrame = 0;
            nextMouthFrame = 0f;
            if (restingMouth) mouth.sprite = restingMouth;
        }
    }

    private float RandomInterval(Vector2 range)
    {
        float low = Mathf.Max(0.02f, Mathf.Min(range.x, range.y));
        return Mathf.Lerp(low, Mathf.Max(low, Mathf.Max(range.x, range.y)), (float)random.NextDouble());
    }

    private void OnDisable()
    {
        if (!initialized) return;
        if (leftEye) leftEye.transform.localPosition = leftHome;
        if (rightEye) rightEye.transform.localPosition = rightHome;
        if (mouth && restingMouth) mouth.sprite = restingMouth;
    }
}
