# Evidence overlay prototype (synthetic only)

This is an isolated, dependency-free prototype for AURA's reviewer-facing evidence
localization. It is deliberately **not wired into extraction, policy, the database, or
the live receipt route**.

## What it proves

- The source image is displayed unchanged. Regions are rendered as an SVG overlay.
- Region coordinates use the normalized `0..1000` coordinate space and remain aligned
  when the image is resized.
- A reviewer can select a field or its region in either direction, with keyboard support.
- An uncertain/missing region is shown as `Khong dinh vi duoc`; the UI never invents a box.
- The synthetic receipt contains both a subtotal and a final payable amount. The
  `totalAmount` region targets only the final payable row.
- The overlay can be disabled without changing any extracted fact or decision.

## Run locally

Open `index.html` directly. For browser QA with a loopback URL, run `node serve.mjs` and
open `http://127.0.0.1:8765/`. No external network request or package install is required.

## Deliberate non-goals

This prototype does not claim OCR/localization accuracy. Production integration still
requires word-level OCR boxes, deterministic evidence-text matching, image-hash binding,
authorization on the receipt route, an annotated development set, and a separately locked
unseen evaluation set. See `docs/EVIDENCE_LOCALIZATION_PLAN_2026-10-13.md`.

