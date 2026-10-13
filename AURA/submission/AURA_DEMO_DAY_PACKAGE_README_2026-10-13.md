# AURA Demo Day package - read first

## What is ready

- bilingual Vietnamese and English proposal in editable DOCX and visually verified PDF;
- exactly five presentation slides in PPTX;
- one-page Build Log in DOCX and PDF;
- public repository, live URL, health endpoint, Verify cases, workflow spec, runbook, and evidence index;
- product-only three-minute video script;
- fifteen-minute presentation script and concise judge Q&A;
- curated evidence package without raw receipt images, API keys, private databases, or billing screenshots.

## What remains a human action

1. Record and upload the official MP4 at or below 180 seconds.
2. Complete three consented real-user sessions and record real names, roles, confirmed verbatim feedback, one negative effect, and one feedback-derived product change.
3. Replace the pending video link and confirm the frozen release ref in the final submission links.
4. Confirm the latest Team Drive deadline and permissions in an incognito browser.

## Presentation story

The product story is not “the model read every receipt.” The turning point was a missed escalation where schema-valid JSON was semantically wrong. AURA then separated identifiers, raw dates, printed final totals, validation, and deterministic policy. The result is a supervised system that knows when to stop and ask a human, while preserving Audit and Undo.

## Official numbers to use

- Release build: 0 warnings, 0 errors.
- Offline tests: 163/163 passed.
- Live Verify: 5/5 in 65.004 seconds.
- Prospective ten-receipt mini-holdout: 8/10, zero missed, two fail-safe over-escalations, no observed system/provider/429 errors.
- Sequential known-data repeat: 11/12, zero missed, one over; stability evidence only.
- Five distinct concurrent uploads: 5/5 completed; this demonstrates queue integrity, not the Verify latency gate or a production SLA.

## Do not claim

- production accuracy or unattended rollout readiness;
- that 163 tests means 163 receipts;
- that the 11/12 repeat is a new holdout;
- production evidence highlighting, distributed provider limiting, production RBAC, safe PDF, malware scanning, or object storage;
- completed three-user evidence until signed records exist.

## Folder map

- `Evidence`: curated technical and evaluation evidence plus SHA-256 manifest.
- `Gia_Phu/OnTap`: personal study map, source-code flow, architecture, business rules, provider, capacity, holdout, and on-site operations.
- `Gia_Phu/Pitching_QnA`: final pitch, likely judge questions, and concise answer patterns.
- `kich_ban_video`: product-only recording script and pending-video gate.
- `kich_ban_thuyettrinh_slide`: slide deck plus fifteen-minute spoken script.
- `Nop`: upload-ready submission structure; pending human artifacts are explicitly labelled rather than fabricated.
