using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

// Demo mode for a build with no dialogue service: the player chooses from a few lines and
// the room answers with replies recorded from the live model
// (Deployment/record_offline_dialogue.py). A line is offered only once the player has heard
// or seen what it is about, and a stage closes where the recorded reply closed it.
// Asking from an object ("Ask the room") offers only the lines about that object; when it has none
// left, a button turns to the general conversation. Opening the conversation on its own (Enter)
// offers only the stage's own lines: small talk, asking for direction, the understanding, settling.
// Exploration, recordings, stage changes and the ending run as they do with the service.
public partial class RoomConversationUI
{
    [Serializable] private sealed class OfflineWords { public string zh, en; public string Current => RoomLocalization.IsChinese ? zh : en; }
    [Serializable] private sealed class OfflineSaid { public string text, reveal; public bool closes; }
    [Serializable] private sealed class OfflineReply { public string id, variant; public OfflineSaid zh, en; public OfflineSaid Current => RoomLocalization.IsChinese ? zh : en; }
    [Serializable] private sealed class OfflineLine { public string id, kind, memory, about, needs, after; public int stage; public string[] match, requires; public OfflineWords ask; }
    [Serializable] private sealed class OfflineDialogue { public OfflineWords opening; public OfflineLine[] lines; public OfflineReply[] replies; }
    private sealed class OfflineOption { public OfflineLine line; public OfflineReply reply; public string key; }

    private const string TeapotNote = "Inside the opened teapot, handwriting on the inner bottom reads: 'Mom, the water is so cold.' Who wrote it, when, and what happened are unknown.";
    // Which lines come first when there are more than fit on one page.
    private static readonly string[] OfflineOrder = { "follow", "object", "found", "care", "doubt", "settle", "insight", "hint", "talk" };

    private bool offlineDemo;
    private OfflineDialogue offlineDialogue;
    private readonly Dictionary<string, OfflineReply> offlineReplies = new Dictionary<string, OfflineReply>();
    private int offlineStage = 1, offlinePage;
    private bool offlineSaidEarly;
    private ObjectRecord offlineFocus; // the object this conversation was opened from, until the panel closes
    private readonly HashSet<string> offlineAsked = new HashSet<string>();
    private GameObject offlinePrompts;
    private Text offlineNote;
    private string offlineShown;

    private bool UseOfflineDemo()
    {
        var asset = Resources.Load<TextAsset>("Demo/OfflineDialogue");
        offlineDialogue = asset != null ? JsonUtility.FromJson<OfflineDialogue>(asset.text) : null;
        offlineDemo = offlineDialogue != null && offlineDialogue.lines != null && offlineDialogue.replies != null
            && offlineDialogue.lines.Length > 0 && offlineDialogue.replies.Length > 0;
        if (!offlineDemo) { Debug.LogError("Offline demo requested, but Resources/Demo/OfflineDialogue has no recorded replies."); return false; }
        foreach (var reply in offlineDialogue.replies) offlineReplies[reply.id + "|" + reply.variant] = reply;
        return true;
    }

    private void BuildOfflinePrompts()
    {
        offlinePrompts = new GameObject("OfflinePrompts", typeof(RectTransform));
        offlinePrompts.transform.SetParent(conversationPanel.transform, false);
        SetAnchors(offlinePrompts, new Vector2(.035f, .09f), new Vector2(.965f, .31f));
        offlineNote = Label(conversationPanel.transform, "DemoNote", "Demo mode  ·  recorded replies", 18, new Vector2(.5f, .005f), new Vector2(.965f, .08f));
        offlineNote.alignment = TextAnchor.MiddleRight;
        offlinePrompts.SetActive(false);
        offlineNote.gameObject.SetActive(false);
    }

    private void StartOfflineSession()
    {
        requestInFlight = false;
        connectionFailed = false;
        sessionId = "offline-demo";
        offlineStage = 1;
        SetJournalStatus("");
        AppendJournalEntry("Room", offlineDialogue.opening.Current);
        ApplyStage(1);
        ExitTypingMode();
    }

    private bool OfflineAsked(string id) => offlineAsked.Any(key => key.StartsWith(id + "|"));

    private List<OfflineOption> OfflineOptions(ClueInteractionController clues)
    {
        var heard = clues != null ? clues.GetRecoveredMemoryIds() : new string[0];
        var seen = new HashSet<string>(clues != null ? clues.GetDiscoveries().Select(d => d.discovery_id) : new string[0]);
        if (objectRecords.Any(record => record.details.Contains(TeapotNote))) seen.Add("teapot_note");
        var here = offlineDialogue.lines.Where(line => line.stage == offlineStage).ToArray();
        // Explored as the service requires before a stage can close.
        bool explored = clues != null && clues.IsCurrentStageComplete
            && (offlineStage != 2 || (seen.Contains("cup_bottom_cracks") && seen.Contains("paper_reverse_crossed_words")));
        var settle = here.FirstOrDefault(line => line.kind == "settle");
        bool settled = settle == null || !offlineReplies.ContainsKey(settle.id + "|") || OfflineAsked(settle.id);
        string tier = offlineStage <= 2 ? "tier1" : offlineStage <= 4 ? "tier3" : "tier5";
        string title = offlineFocus != null ? offlineFocus.title.ToLowerInvariant() : null;
        string focus = offlineFocus != null && clues != null ? clues.MemoryIdOf(offlineFocus.id) ?? "" : null;

        var options = new List<OfflineOption>();
        foreach (var line in offlineDialogue.lines)
        {
            if (line.stage != 0 && line.stage != offlineStage) continue;
            // Lines kept for any stage (ordinary objects, the letter) belong to their object: they are
            // offered only when asking from it, and the letter is talked about once, not again each stage.
            if (line.stage == 0 && (focus == null || (line.kind == "found" && OfflineAsked(line.id)))) continue;
            // A line about an object is said only while asking from that object.
            if (!string.IsNullOrEmpty(line.about) && line.about != focus) continue;
            string variant = line.stage == 0 ? tier : "";
            bool offered;
            switch (line.kind)
            {
                case "talk": offered = line.requires == null || line.requires.All(item => heard.Contains(item) || seen.Contains(item)); break;
                case "care": case "doubt": offered = heard.Contains(line.memory) && (string.IsNullOrEmpty(line.needs) || seen.Contains(line.needs)); break;
                case "follow": offered = OfflineAsked(line.after); break;
                case "found": offered = seen.Contains(line.needs) && (string.IsNullOrEmpty(line.memory) || heard.Contains(line.memory)); break;
                case "object": offered = title != null && line.match.Any(word => word.StartsWith("=") ? title == word.Substring(1) : title.Contains(word)); break;
                // Offered once what it refers to has been heard or seen. Said before the stage is
                // explored, the understanding is held; it closes the stage afterwards.
                case "insight":
                    offered = line.requires.All(item => heard.Contains(item) || seen.Contains(item))
                        && (explored ? !offlineSaidEarly || settled : !offlineSaidEarly);
                    variant = explored ? "final" : "early";
                    break;
                case "settle": offered = offlineSaidEarly && explored; break;
                // "I don't know where to look": the reply recorded for exactly the recordings still unheard.
                case "hint":
                    var unheard = here.Where(item => item.kind == "care" && !heard.Contains(item.memory)).Select(item => item.memory).Distinct().ToArray();
                    offered = unheard.Length > 0;
                    variant = string.Join("+", unheard);
                    break;
                default: offered = false; break;
            }
            string key = line.id + "|" + variant;
            if (offered && !offlineAsked.Contains(key) && offlineReplies.TryGetValue(key, out var reply))
                options.Add(new OfflineOption { line = line, reply = reply, key = key });
        }
        if (focus != null)
        {
            var aboutThis = options.Where(option => option.line.kind == "object" || (!string.IsNullOrEmpty(option.line.about) && option.line.about == focus)).ToList();
            // Only this object's lines. Small talk, asking for direction, the understanding and settling
            // belong to the conversation opened on its own (Enter, or "Talk about something else").
            options = aboutThis;
        }
        return options.OrderBy(option => Array.IndexOf(OfflineOrder, option.line.kind)).ToList();
    }

    private void RefreshOfflinePrompts(bool inspecting)
    {
        bool visible = offlineDemo && !inspecting && !recordsPage;
        offlineNote.gameObject.SetActive(visible);
        offlinePrompts.SetActive(visible);
        if (!offlineDemo) return;
        // Says which object the lines below are about.
        // Says which conversation this is: about one object, or with the room itself.
        topicLabel.gameObject.SetActive(visible);
        topicLabel.text = offlineFocus == null ? "Talking with the room" : "Asking about: " + offlineFocus.title;
        if (!visible) return;
        SetAnchors(topicLabel.gameObject, new Vector2(.035f, .315f), new Vector2(.965f, .37f));
        bool canAsk = !string.IsNullOrEmpty(sessionId) && !storyEnded && !requestInFlight && pendingStage == 0 && !pendingEnding;
        var options = canAsk ? OfflineOptions(Clues) : new List<OfflineOption>();
        // Three lines and a way to the rest, when there are more than four.
        int perPage = options.Count > 4 ? 3 : 4, pages = Mathf.Max(1, Mathf.CeilToInt(options.Count / (float)perPage));
        offlinePage %= pages;
        var page = options.Skip(offlinePage * perPage).Take(perPage).ToList();
        string shown = RoomLocalization.Language + canAsk + (offlineFocus != null) + offlinePage + "/" + pages + string.Join(",", page.Select(option => option.key));
        if (shown == offlineShown) return;
        offlineShown = shown;
        foreach (Transform old in offlinePrompts.transform) { old.gameObject.SetActive(false); Destroy(old.gameObject); }
        if (!canAsk) return;
        if (options.Count == 0)
        {
            // Nothing left about this object: carry on talking about other things instead of stopping.
            // In the last slot, away from where the player just clicked, so it is not pressed by accident.
            if (offlineFocus != null) OfflineButton(3, "SomethingElse", "Talk about something else…", () => { LastUIActionFrame = Time.frameCount; offlineFocus = null; offlinePage = 0; });
            else Label(offlinePrompts.transform, "Nothing", "Explore the room, then come back to ask.", 22, Vector2.zero, Vector2.one);
            return;
        }
        for (int slot = 0; slot < page.Count; slot++)
        {
            var option = page[slot];
            OfflineButton(slot, "Line" + slot, option.line.ask.Current, () => AskOffline(option));
        }
        if (pages > 1) OfflineButton(3, "More", UIWords("其他的话  ›", "More  ›"), () => { LastUIActionFrame = Time.frameCount; offlinePage++; });
    }

    private void OfflineButton(int slot, string name, string words, UnityEngine.Events.UnityAction action)
    {
        float left = slot % 2 == 0 ? 0 : .505f, bottom = slot < 2 ? .52f : 0;
        var button = Button(offlinePrompts.transform, name, words, new Vector2(left, bottom), new Vector2(left + .495f, bottom + .48f), action);
        var label = button.GetComponentInChildren<Text>();
        label.resizeTextForBestFit = true; label.resizeTextMinSize = 14; label.resizeTextMaxSize = label.fontSize;
    }

    private void AskOffline(OfflineOption option)
    {
        LastUIActionFrame = Time.frameCount;
        if (requestInFlight || storyEnded || pendingStage != 0 || !offlineAsked.Add(option.key)) return;
        StartCoroutine(AnswerOffline(option));
    }

    private IEnumerator AnswerOffline(OfflineOption option)
    {
        requestInFlight = true;
        var said = option.reply.Current;
        AppendJournalEntry("You", option.line.ask.Current);
        SetJournalStatus("The room is listening…");
        yield return new WaitForSecondsRealtime(1.4f);
        SetJournalStatus("");
        AppendJournalEntry("Room", said.text + (string.IsNullOrEmpty(said.reveal) ? "" : "\n\n" + said.reveal));
        requestInFlight = false;
        offlinePage = 0;
        if (option.line.kind == "insight" && !said.closes) offlineSaidEarly = true;
        if (!said.closes) yield break;
        offlineSaidEarly = false;
        if (offlineStage < 6)
        {
            offlineStage++;
            ApplyReplyStage(offlineStage, true, "offline_demo");
        }
        else
        {
            storyEnded = true;
            QueueStoryEnding();
        }
    }
}
