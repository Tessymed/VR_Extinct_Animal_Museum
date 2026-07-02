using System.Collections;
using UnityEngine;
using TMPro;

[RequireComponent(typeof(CanvasGroup))]
public class VRExhibitPanel : MonoBehaviour
{
    [Header("UI Text Components")]
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI originText;
    public TextMeshProUGUI extinctText;
    public TextMeshProUGUI causeText;
    public TextMeshProUGUI lessonText;

    [Header("Fade & Animation Settings")]
    [Tooltip("How fast the panel fades in (seconds).")]
    public float fadeDuration = 1.0f;

    [Tooltip("Delay between revealing each line of text (seconds).")]
    public float revealStaggerDelay = 0.5f;

    private CanvasGroup canvasGroup;
    private Coroutine activationCoroutine;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        
        // Hide panel at start
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        // Hide text elements initially to support sequential reveal
        SetTextAlpha(titleText, 0f);
        SetTextAlpha(originText, 0f);
        SetTextAlpha(extinctText, 0f);
        SetTextAlpha(causeText, 0f);
        SetTextAlpha(lessonText, 0f);
    }

    /// <summary>
    /// Begins the panel activation and fade-in sequence.
    /// </summary>
    public void ActivatePanel()
    {
        if (activationCoroutine != null)
        {
            StopCoroutine(activationCoroutine);
        }
        activationCoroutine = StartCoroutine(ActivationSequence());
    }

    /// <summary>
    /// Resets the panel back to its hidden state.
    /// </summary>
    public void ResetPanel()
    {
        if (activationCoroutine != null)
        {
            StopCoroutine(activationCoroutine);
        }
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        SetTextAlpha(titleText, 0f);
        SetTextAlpha(originText, 0f);
        SetTextAlpha(extinctText, 0f);
        SetTextAlpha(causeText, 0f);
        SetTextAlpha(lessonText, 0f);
    }

    private IEnumerator ActivationSequence()
    {
        // 1. Fade in the background / main canvas
        float elapsedTime = 0f;
        while (elapsedTime < fadeDuration)
        {
            elapsedTime += Time.deltaTime;
            canvasGroup.alpha = Mathf.Clamp01(elapsedTime / fadeDuration);
            yield return null;
        }
        canvasGroup.alpha = 1f;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;

        // 2. Stagger-reveal each text element
        yield return StartCoroutine(FadeInText(titleText, 0.4f));
        yield return new WaitForSeconds(revealStaggerDelay);

        yield return StartCoroutine(FadeInText(originText, 0.3f));
        yield return new WaitForSeconds(revealStaggerDelay);

        yield return StartCoroutine(FadeInText(extinctText, 0.3f));
        yield return new WaitForSeconds(revealStaggerDelay);

        yield return StartCoroutine(FadeInText(causeText, 0.3f));
        yield return new WaitForSeconds(revealStaggerDelay);

        yield return StartCoroutine(FadeInText(lessonText, 0.5f));
    }

    private IEnumerator FadeInText(TextMeshProUGUI text, float duration)
    {
        if (text == null) yield break;

        float elapsedTime = 0f;
        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            SetTextAlpha(text, Mathf.Clamp01(elapsedTime / duration));
            yield return null;
        }
        SetTextAlpha(text, 1f);
    }

    private void SetTextAlpha(TextMeshProUGUI text, float alpha)
    {
        if (text != null)
        {
            Color color = text.color;
            color.a = alpha;
            text.color = color;
        }
    }
}
