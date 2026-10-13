# AURA final pitch script and judge Q and A

## Recommended 15-minute allocation

Aim for 13 minutes 30 seconds and keep 90 seconds for transitions or a short interruption.

| Time | Segment | Outcome |
|---:|---|---|
| 0:00–1:15 | Slide 1 and product story | Explain why the problem is the decision boundary, not OCR alone |
| 1:15–2:45 | Slide 2 | Show the workflow and the two explicit human decisions |
| 2:45–4:25 | Slide 3 | Present honest evidence and measurement method |
| 4:25–5:35 | Slide 4 | Explain the real architecture and simulated data |
| 5:35–6:35 | Slide 5 | State failure behavior, limitations, and roadmap |
| 6:35–9:33 | Official product demo video or equivalent live sequence | Demonstrate Verify, escalation, manager decision, Audit, and Undo |
| 9:33–11:20 | Evidence interpretation | Explain over-escalation, concurrency, and claim boundaries |
| 11:20–12:30 | User evidence | Present completed sessions only; otherwise state the gate is pending |
| 12:30–13:30 | Conclusion | State current go/no-go and the next controlled pilot |
| 13:30–15:00 | Buffer | Screen switching or brief judge interaction |

## The product story

### Opening

“When I started AURA, I thought the hard part was reading receipts. The first real-receipt holdout changed that view. A model can return valid JSON and still assign the wrong meaning to a number or identifier. The real problem is deciding when evidence is sufficient and when automation must stop.”

“That failure changed the product. I separated vision extraction from approval policy, added evidence provenance and semantic checks, limited repair to one attempt, and kept humans at the exception boundary. AURA became less about a clever model output and more about an accountable workflow.”

This story is supported by the project history and evidence contract changes. Do not add an invented employer anecdote or claim that real accounting users requested a feature unless a user-study record exists.

## Slide 1

“A reimbursement request passes through receipt submission, manual reading, policy comparison, manager questions, and record keeping. The bottleneck is not only OCR. It is the absence of a consistent rule for when the evidence is sufficient.”

“AURA assigns narrow responsibilities. Qwen reads facts. Deterministic C# policy decides whether the claim can continue. People answer exceptions. The system keeps the timeline.”

## Slide 2

“Upload validates the file, persists a PENDING request, and returns HTTP 202. A SQL-backed worker claims the job with a lease, so a refresh or restart does not delete the work. The model returns structured facts, semantic validation may request one repair, and policy creates the final route.”

“The first human decision is the employee confirming the handoff. The second is the manager answering a specific question. Undo adds an event rather than deleting history.”

## Slide 3

“The current candidate has a clean Release build and 163 of 163 offline tests. These are code tests, not 163 receipts. The live Verify Harness passed five of five cases in 65.004 seconds.”

“The prospective ten-receipt mini-holdout achieved eight of ten exact decisions, zero missed escalations, two fail-safe over-escalations, and no observed provider or system errors. I do not present this as production accuracy. A later repeat on known data reached eleven of twelve and is labelled stability evidence.”

“The pilot measures touchless rate, review time, missed escalation, over-escalation, assistance, and negative user impact. Only completed user evidence belongs on the final pitch.”

## Slide 4

“AURA runs as an ASP.NET Core vertical slice. SQL stores requests, jobs, results, and audit. OpenRouter routes Qwen3.8 Flash to Alibaba for extraction. The policy engine remains independent from the model.”

“The five Verify images and extended test kits are synthetic and disclosed. Upload, provider calls, validation, policy, review workflow, and audit run through the real application.”

## Slide 5

“A provider timeout or 429 never becomes an approval. Fallback can run only when enabled and only for allowed infrastructure errors. Schema or semantic failures get one repair attempt, then stop as FACT. The live host keeps fallback disabled.”

“Current limits include one JPG or PNG, no production RBAC, no safe multipage PDF pipeline, no managed object storage, and no distributed provider limiter. Evidence localization is still an isolated prototype, not a production explanation claim.”

## Evidence interpretation after the demo

“Five different concurrent uploads completed five of five without losing a request. Their E2E P95 was 93.684 seconds because one AI permit serialized inference. That measurement is the queue-integrity gate, not the Verify latency gate.”

“FAHASA is the stable remaining over-escalation. The model reads the important fields, but the validator does not yet accept the label ‘Thanh toán’ as final-payable evidence. Adding that phrase broadly could confuse a payment-method line with a final total, so I froze the candidate and require independent positive and negative fixtures before changing the guard.”

## Core judge questions

### Why does AURA need AI if C# policy makes the decision?

Receipt layouts and visible wording vary. The vision model converts that visual input into structured facts and evidence. C# then applies a stable, testable organizational rule. AI handles perception; code and people retain authority.

### Why not let the model approve directly?

A schema-valid response can still be semantically wrong. AURA therefore checks field roles, evidence, amounts, dates, and identifiers. Giving approval authority to the same component that may misread the receipt would remove an independent control.

### Why is FACT higher priority than POLICY or AUTHORITY?

Policy and authority conclusions depend on reliable facts. When the total, date, merchant, or identifier is uncertain, AURA first asks a human to establish the facts instead of asserting a downstream conclusion.

### Are 163 tests the same as 163 receipts?

No. They are offline unit and integration tests for policy, contracts, queue behavior, fallback, audit, and UI contracts. Receipt accuracy is reported separately on locked datasets.

### Why is 8/10 acceptable?

It is not sufficient for unattended production. It is useful evidence for a supervised MVP because there were no missed escalations or system errors and both mismatches failed safe. The two over-escalations remain an operational cost that must be reduced on unseen data.

### Did you rerun the holdout until the number improved?

No. The prospective ten-case package ran once after labels and hashes were locked. Later runs on known bytes are explicitly labelled regression or stability evidence and never replace the prospective result.

### Why not fix FAHASA before the final?

The obvious keyword patch could interpret tender or cash-given lines as final totals. A safe change needs positive final-payable fixtures and negative tender, change, and subtotal fixtures. Freezing a transparent over-escalation is safer than introducing a fail-open defect.

### What happens when OpenRouter returns 429?

The error is classified as infrastructure failure, the shared AI permit and cooldown prevent request amplification, and the request stops as system escalation when live fallback is disabled. AURA never fabricates a business decision.

### Can five judges upload at once?

Five distinct uploads were accepted into SQL and completed without loss. Inference is serialized at concurrency one, so later requests wait. That proves queue integrity on the single-process demo, not a production latency SLA or multi-instance capacity.

### Why does a second Verify receive 409?

Verify is an exclusive benchmark operation. A process-local operation gate allows one real batch and rejects duplicate concurrent clicks so five judges do not multiply the same five AI calls.

### Is SemaphoreSlim still the main upload queue?

No. SQL rows are the durable job source and a bounded channel only wakes the worker. Process-local gates remain for the Verify operation and provider capacity. They do not replace a distributed limiter when the application scales to several processes.

### Is Ollama ready as live fallback?

It is a supervised local contingency, not the public-host fallback. The locked 4B profile passed a synthetic quality gate but had high latency. SmarterASP cannot call the laptop through loopback, so fallback remains disabled on live.

### What does HTTP 202 mean here?

The server has validated and persisted the request, not completed AI processing. The client polls the status URL while the worker processes the durable SQL job.

### Can a refresh or recycle lose a claim?

The in-memory signal may disappear, but the PENDING SQL record remains. A lease can expire and another worker cycle can reclaim the job. This has been covered by tests and live persistence checks.

### Is the audit log immutable?

The application appends events and Undo does not delete history. It is not yet a compliance-grade immutable store. Production needs restricted append permissions, external retention, and WORM or hash-chain controls.

### Does SHA-256 prove a receipt is genuine?

No. It proves byte identity and protects the evidence package from silent file changes. It does not verify issuer legitimacy or detect the same content after resize or recapture.

### Why exclude the claimed amount from the prompt?

It prevents the model from anchoring on the employee's answer. The model reads the printed total independently, and backend policy compares the two values afterward.

### Can AURA safely accept PDF?

Not by adding an extension. A safe slice needs signature and MIME checks, page and size limits, malware scanning, sandboxed rasterization, page-level provenance, aggregation rules, retention, and a new benchmark.

### Can the evidence overlay be shown as production explainability?

No. The synthetic prototype proves the interaction design and hash-bound region contract. It still needs word-level OCR, independent annotation, an unseen set, and region-recall and false-highlight measurement. It does not affect policy today.

### What is the biggest feature you cut?

Production evidence localization. It could improve reviewer confidence, but without annotated word boxes it could highlight the wrong amount and create false confidence. The prototype remains isolated until it has its own evaluation gate.

### What is the biggest current submission risk?

The three-user evidence gate. The technical candidate and required artifacts can be prepared, but the track requires real identities, roles, verbatim feedback, one traceable change, and one negative impact. The team must collect that evidence honestly before claiming completion.

## Closing

“AURA does not try to replace the approver. It turns a receipt into traceable facts, uses deterministic policy to route the claim, stops on uncertainty, and keeps the human decision auditable. The current evidence supports a supervised demonstration. The next step is a controlled user pilot, not an unattended production claim.”
