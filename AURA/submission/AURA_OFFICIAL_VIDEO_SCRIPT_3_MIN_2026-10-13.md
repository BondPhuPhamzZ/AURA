# AURA official demo video script

## Recording decision

Submit one product-only demo video between 2 minutes 45 seconds and 2 minutes 55 seconds. Do not present slides in this video. Keep a lower-bitrate backup copy for the competition laptop or USB, but submit only one official video item.

Use synthetic cases or a receipt explicitly cleared for publication. Do not show API keys, provider billing, hosting control panels, connection strings, private evidence folders, or real personal data. Record the live URL and do not edit out provider wait time to make the system look faster.

## Final timeline and voiceover

| Time | On-screen action | Suggested voiceover |
|---:|---|---|
| 0:00–0:12 | Show the live URL and the first-action instruction on AURA home | “AURA is an Escalation Referee for reimbursement. The vision model reads receipt facts, deterministic policy decides, and people handle exceptions.” |
| 0:12–0:20 | Select **Chạy Verify Harness** exactly once; the page auto-scrolls to the verification table | “The official harness runs five cases through the real pipeline: three routine approvals and two escalations.” |
| 0:20–1:18 | Keep the progress and timestamp visible while Verify runs | “Each result compares expected and actual behavior. AURA separates uncertain facts, policy exceptions, and authority limits. It never converts a provider failure into a business approval.” |
| 1:18–1:32 | Hold on the final 5/5 table and elapsed time | “This frozen candidate completed 5 of 5 live in 65 seconds, within the 90-second gate.” |
| 1:32–1:48 | Open a prepared synthetic `ESCALATE_*` request and its AI facts/reason | “Here the model only supplies facts. The C# policy stops the claim and asks a concrete question a reviewer can answer.” |
| 1:48–2:08 | Select **Chuyển tiếp**, switch to the manager view, and choose one meaningful Yes or No action | “The employee confirms the handoff. The manager remains accountable for the exception decision.” |
| 2:08–2:28 | Open Audit, show the consolidated timeline, then Undo the latest manager action | “Audit records what happened, when, on which request, and why. Undo appends a new event; it does not erase history.” |
| 2:28–2:42 | Return to the request and point to provider, status, facts, and question | “SQL keeps the background job durable across refresh or restart. The model can change without changing approval policy.” |
| 2:42–2:54 | Keep AURA on screen while stating limits | “This is a supervised MVP: one JPG or PNG, no production RBAC or multipage PDF, and fallback remains off on the live host.” |
| 2:54–2:58 | Hold the AURA title and live URL | “AURA automates clear work and stops where a human decision is required.” |

## Pre-recording checklist

1. Rehearse the full sequence once on the frozen commit without starting a second Verify run.
2. Check `/healthz`: status ok, Qwen3.8 Flash, reasoning none, DB/storage/migration ready, fallback false.
3. Close OpenRouter, hosting, terminal, file explorer, and personal-receipt tabs.
4. Keep one operator. Do not upload another receipt while Verify is running.
5. Use 1920×1080, browser zoom 100%, large cursor, clear audio, and no notifications.
6. Confirm the video duration is at most 180 seconds and that the displayed result really finished.
7. Watch the exported file end to end and scan every frame for secrets and PII.

## Failure plan

If the live request does not begin within about ten seconds, stop the take and investigate privately. Do not hide an incomplete run with editing. For the final presentation, keep this same official video available as the fallback when the live URL or provider is unavailable.
