using System.Collections;
using UnityEngine;

public partial class RoomConversationUI
{
    private int pendingStage;
    private bool pendingEnding;

    private void QueueStoryEnding()
    {
        if (pendingEnding) return;
        pendingEnding = true;
        var clues = FindFirstObjectByType<ClueInteractionController>();
        if (clues != null && clues.IsInspecting) clues.ReturnInspectedObject();
        recordsPage = false;
        RenderJournal();
        SetExpanded(true);
        StartCoroutine(CompleteStoryEnding());
    }

    private IEnumerator CompleteStoryEnding()
    {
        while (expanded || MenuOpen) yield return null;
        yield return new WaitForSecondsRealtime(.6f);
        while (MenuOpen) yield return null;
        FindFirstObjectByType<ClueInteractionController>()?.ApplyStoryEnding();
        pendingEnding = false;
    }

    private void ApplyReplyStage(int nextStage, bool completed, string effect)
    {
        if (completed && nextStage >= 2 && nextStage <= 6) QueueStageOneTransition(nextStage);
        else ApplyStage(nextStage);
    }

    private void QueueStageOneTransition(int nextStage)
    {
        if (pendingStage != 0) return;
        pendingStage = nextStage;
        var clues = FindFirstObjectByType<ClueInteractionController>();
        if (clues != null && clues.IsInspecting) clues.ReturnInspectedObject();
        // A reply arriving while the player explores must still be read before
        // the local room changes. Closing this panel acknowledges the response.
        recordsPage = false;
        RenderJournal();
        SetExpanded(true);
        StartCoroutine(CompleteStageOneTransition());
    }

    private IEnumerator CompleteStageOneTransition()
    {
        while (expanded || MenuOpen) yield return null;
        yield return new WaitForSecondsRealtime(.6f);
        while (MenuOpen) yield return null;
        var light = pendingStage == 2 ? FindBookWarmLight() : null;
        // Stage two closes with a quiet pause and the existing ambient fade.
        // Its marks remain on the props; no extra rendering effects are needed.
        if (light != null)
        {
            float initial = light.intensity;
            float elapsed = 0;
            while (elapsed < 1.4f)
            {
                if(MenuOpen){yield return null;continue;}
                elapsed += Time.unscaledDeltaTime;
                if (light == null) break;
                light.intensity = Mathf.Lerp(initial, initial * .8f,
                    Mathf.SmoothStep(0, 1, elapsed / 1.4f));
                yield return null;
            }
        }
        ApplyStage(pendingStage);
        pendingStage = 0;
        inputField.interactable = !storyEnded && !requestInFlight;
    }

    private Light FindBookWarmLight()
    {
        var book = FindFirstObjectByType<BookLetterClue>();
        if (book == null) return null;
        var light = book.transitionWarmLight;
        if (light != null && light.isActiveAndEnabled && light.intensity > 0
            && light.bakingOutput.lightmapBakeType != LightmapBakeType.Baked) return light;
        Debug.LogWarning("Stage one needs an assigned, active realtime warm light on BookLetterClue.");
        return null;
    }
}
