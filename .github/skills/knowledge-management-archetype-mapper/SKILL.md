---
name: knowledge-management-archetype-mapper
description: Transform domain requirements into a Knowledge Management archetype model. Identifies sources, evidence, confidence, freshness/review cadence, scope, and publication/retention rules for governed knowledge and answer-with-citation systems.
argument-hint: "[domain requirements or feature description]"
---

# Knowledge Management Archetype Mapper

## Enforcement Rules

- ALWAYS run the fit test in "When NOT to Use" before mapping.
- DO NOT produce a mapping if the fit test fails.
- DO NOT ask clarifying questions immediately — run the fit test first.
- ALWAYS ask clarifying questions only after the fit test passes and only when there are business-impacting gaps.
- ALWAYS include "To zależy / It depends" as an explicit option in every AskUserQuestion call.
- NEVER silently assume a policy that changes business behavior.
- NEVER skip the "Unmapped Concepts" section.
- Treat RAG, LLMs, and MCP tools as implementation details, not as the archetype itself.

Transform any domain description that involves managing knowledge with traceability and trust into a knowledge-governance model. The knowledge does not need to be free text — it can be facts, rules, policies, procedures, or derived answers — but the business must care about *where it came from, how fresh it is, and how much to trust it*.

**Output goal**: A complete, implementable model that gives the system source traceability, evidence-backed answers, freshness/review control, and confidence-aware trust — without conflating knowledge governance with document versioning, workflow, or audit logging.

## When to Use

**Use this skill when the domain involves:**
- knowledge bases, FAQs, policies, procedures, manuals, playbooks
- source traceability, evidence, freshness, confidence, scope
- answer generation with citations
- review, approval, and publication of knowledge
- support, compliance, internal enablement, expert systems

**Output is useful for:**
- Domain modeling sessions before implementing a knowledge base, RAG system, or expert-answer feature
- Designing review/approval and freshness policies for governed content
- Clarifying where "search/RAG plumbing" ends and "knowledge governance" begins

## When NOT to Use — Fit Test

Before starting the mapping, apply this test. If the domain fails it, **stop and tell the user** that the Knowledge Management archetype does not fit, and briefly explain why.

### The core question

> *"Can I ask: what do we know about X, from which source, how fresh is it, how trustworthy is it, and can we cite evidence?"*

If **yes** → Knowledge Management likely fits.

If the natural question is:
- **"What state is X in?"** → workflow/state machine. Do not map.
- **"How much X do we have?"** → accounting/resource model. Do not map.
- **"What is the document version?"** → document/version model. Do not map.
- **"What task is pending?"** → workflow/case model. Do not map.

### Signal table

| Signal in requirements | Likely archetype fit? |
|------------------------|-----------------------|
| "answer customer/agent questions from our docs, with citations" | ✅ Yes |
| "content must be reviewed/approved before it goes live" | ✅ Yes |
| "flag articles as outdated after N months" | ✅ Yes |
| "two sources disagree — which one do we trust?" | ✅ Yes |
| "ticket moves from open → assigned → resolved" | ❌ No — workflow/state machine |
| "how many licenses/credits does the customer have" | ❌ No — accounting/resource model |
| "track every change to a contract with diffs" | ⚠️ Borderline — see below |
| "prove what actions were taken for a compliance audit" | ⚠️ Borderline — see below |
| "make our existing docs full-text searchable" | ⚠️ Borderline — see below |

### Borderline cases — how to decide

- **Document/version model vs. Knowledge Management**: A repository of versioned documents is a document/version archetype *unless* the business also needs a truth judgment across sources — i.e., the versioning itself isn't the point, "is this still correct and can we prove it" is. If nobody asks "do we still trust this," it's just versioning.
- **Audit trail vs. Knowledge Management**: An audit log records what happened — it is evidence *for* something, not knowledge that is actively curated, reviewed, and served as an answer. If the ask is "prove what we did," it's audit. If the ask is "what do we currently believe/recommend," it's Knowledge Management.
- **Search/index feature vs. Knowledge Management**: Making existing docs searchable with no governance concerns (no ownership, no freshness, no trust question) is a technical search feature, not this archetype. If any governance question exists ("who owns this," "can it expire," "can we trust it"), it fits.

### If the domain does not fit

Output only:

```
## Archetype Fit Assessment: ❌ Does Not Fit

The Knowledge Management archetype requires a governed body of knowledge where source,
freshness, and trust are business concerns. This domain is a [state machine / accounting /
document-version / audit / pure search] because:

- [specific reason from the requirements]
- The natural question is "[state/quantity/version/task]", not "what do we know and how much
  do we trust it".
```

Do NOT suggest alternative patterns or architectures. Stop here.

---

## Mapping Workflow

### Step 0: Get Requirements

- If provided as argument, use it directly.
- If not provided, scan the recent conversation for domain context. If found, use that.
- Only if no argument AND no context in session, ask:
  > "Describe the domain — what knowledge is being managed, where does it come from, and who relies on it?"

---

### Step 1: Identify the Knowledge Value

Detect what kind of knowledge the business is managing.

**Detection signals:**
- Nouns that represent claims, rules, or procedures treated as authoritative
- Content that gets verified, approved, or cited as a source of truth
- Answers that must be traceable back to origin material

**Examples:** facts, business rules, policies, procedures, manuals, FAQs, evidence, derived/generated answers

**Key question to answer:** *What kind of knowledge is this, and who is the authority on it?*

**Output:** Named domain knowledge value (e.g., `SUPPORT_KNOWLEDGE`, `COMPLIANCE_POLICY_SET`) with a one-line description of its authority source.

---

### Step 2: Ask Clarifying Questions

Ask only when the requirements leave a business-impacting gap. Frame every question as a **design choice**, not an assumed default — the answer may be "yes for some knowledge, no for others". Batch questions into a single `AskUserQuestion` call (up to 4 questions per call; split into multiple calls if more are needed).

#### Category A — Standard knowledge-governance decisions

Ask only about those **not clearly addressed** in the requirements:

- **Source ownership**: who owns/produces each source, and is ownership exclusive or shared?
- **Approval / publication gate**: must knowledge pass review/approval before it is visible, or can it be published directly?
- **Freshness / review cadence**: does knowledge go stale, and on what schedule (calendar, rolling, or event-triggered)?
- **Confidence / trust**: are some sources more trusted than others, and is trust set per-source or per-assertion?
- **Scope / audience**: is knowledge visible to everyone, or scoped by role/tenant/region?
- **Evidence for answers**: must a derived answer cite the specific source/section, or is an unsourced answer acceptable?
- **Stale-content handling**: when knowledge is outdated, is it hidden, deprecated, or archived — and by whom?

#### Category B — Gap-triggered questions

Scan the requirements for anything the archetype supports but the requirements do not mention. For each gap found, ask whether that dimension is wanted. Do not limit yourself to the list above — reason freely. Examples:

- **Conflict resolution**: if multiple sources can disagree, is there an explicit precedence/reconciliation rule, or is it decided case by case?
- **Source retraction**: if a source is retracted or corrected, what happens to already-published knowledge derived from it?
- **Confidence threshold**: is there a minimum confidence below which knowledge cannot be published or served as an answer?
- **Steward accountability**: is a named owner required for every knowledge item, or are unowned items allowed?

Collect answers before proceeding. If the user cannot answer, document the assumption in **Implementation Notes**.

#### Handling "it depends / both / varies by situation" answers

Always include **"To zależy / It depends"** as an explicit option in every `AskUserQuestion` call — do not rely on the automatic "Other" fallback. If the user selects it, treat it as a **variable policy**:

- Document the *parameter* the model will accept (e.g., `review_cadence`, `trust_weight`, `publication_gate`)
- Note in **Implementation Notes** that its value is computed externally by a policy/business-rules layer and passed in
- Do **not** attempt to model the decision logic inside the archetype

---

### Step 3: Map Domain Concepts

For each significant noun and verb in the requirements, produce an explicit mapping table:

```
| Domain Concept       | Knowledge Management Concept              | Notes |
|-----------------------|-------------------------------------------|-------|
| [domain noun/verb]    | Knowledge Item / Assertion / Source / Evidence / Relation / Confidence / Freshness / Scope / Owner / Review-Approval / Publication-Retention / Derived Answer | [why] |
```

After the table, list any domain concepts that **could not be mapped**:

```
## Unmapped Concepts

The following domain concepts have no clear Knowledge Management archetype equivalent:
- [concept] — [reason it doesn't fit / decision needed]
```

This section must be present even if empty (`None identified`).

**Core concept definitions** (use consistently across mappings):

| Concept | Definition |
|---------|------------|
| Knowledge Item | A discrete unit of managed knowledge (article, policy, procedure, FAQ entry) |
| Knowledge Assertion | A specific claim asserted to be true within an item |
| Source | The origin system, document, or person the knowledge came from |
| Evidence | Concrete proof backing an assertion (citation, excerpt, data point) |
| Relation | A link between items/assertions (supersedes, contradicts, depends on) |
| Confidence | The trust level assigned to an assertion or item |
| Freshness | How current/valid the knowledge is right now |
| Scope | The audience or domain boundary the knowledge applies to |
| Owner / Steward | The accountable party responsible for correctness and lifecycle |
| Review / Approval | The gate a knowledge item must pass before publication or after a freshness trigger |
| Publication / Retention | The lifecycle state controlling visibility and eventual archival |
| Derived Answer | A generated response built from one or more assertions, carrying their evidence and confidence |

---

### Step 4: Identify Knowledge Containers

Determine where knowledge lives — the containers.

**Detection signals:**
- Distinct ownership or audience boundaries for different knowledge sets
- Separate sources feeding the same knowledge domain (internal docs vs. vendor docs vs. support tickets)
- A governed vs. ungoverned split (official policy set vs. informal team notes)

**Container types to consider:**

| Type | Purpose | Example |
|------|---------|---------|
| Knowledge base | Primary governed store | `support_kb` |
| Topic collection | Grouping within a KB | `billing_faqs` |
| Source repository | Raw ingested material, pre-governance | `vendor_manual_repo` |
| Policy set | Rules requiring formal approval | `compliance_policies` |
| Indexed corpus | Searchable representation used to generate answers | `answer_index` |

For each container, define:
- **Owner/steward**
- **Scope/audience**
- **Governance level**: governed (reviewed/approved) vs. ungoverned (raw/unverified)

---

### Step 5: Identify Knowledge Operations

Find all business operations that change knowledge state.

**Detection signals:** verbs in the requirements — ingest, classify, verify, approve, publish, revise, deprecate, archive, answer, cite, escalate, reconcile

| Operation | Typical Trigger | Reversible? |
|-----------|-----------------|-------------|
| ingest | New source material becomes available | N/A — creates draft state |
| classify | Ingested item needs categorization | Yes — reclassify |
| verify | Steward checks assertion against evidence | Yes — re-verify |
| approve | Review gate passed | Conditional — can be revoked |
| publish | Approved item made visible to its scope | Yes — unpublish |
| revise | New evidence supersedes prior content | No — creates new version; old one is superseded, not deleted |
| deprecate | Freshness policy or steward marks item outdated | Conditional — can be reinstated if re-verified |
| archive | Retention policy or deprecation window elapses | No — retained but not actively surfaced |
| answer | A query requests knowledge | N/A — read operation |
| cite | Answer references specific evidence | N/A — read operation |
| escalate | Confidence too low or sources conflict | Yes — de-escalate once resolved |
| reconcile conflicting sources | Two sources disagree on the same assertion | N/A — produces a decision, not itself reversible |

Not every operation applies to every domain — include only those the requirements support, and add any domain-specific operations found.

---

### Step 6: Define Traceability and Freshness

For each knowledge object identified in Steps 3–4, define:
- **Source** — where it came from
- **Evidence** — what backs the assertion
- **Confidence** — trust level and how it's determined
- **valid_from / valid_to or review_due** — freshness window
- **Freshness policy** — review cadence and trigger for staleness
- **Publication status** — draft / in review / published / deprecated / archived

---

### Step 7: Decision Sanity Check

**Before producing the final output**, enumerate every concrete decision embedded in the draft model and verify each one has a source:

- **(R)** — explicitly stated in the requirements
- **(A)** — asked and answered in Step 2
- **(X)** — neither: assumed silently

**Decision checklist:**

| Decision area | Example decisions to check |
|---------------|---------------------------|
| Source ownership | Is every source's owner named? Can ownership be shared? |
| Approval flow | Does every container require approval, or only some? |
| Conflicting sources | Is there an explicit reconciliation rule, or is it ad hoc? |
| Freshness | Does every object have a review cadence? What happens on expiry? |
| Confidence | Is confidence assigned per-source, per-assertion, or both? |
| Scope | Is every container's audience explicit? |
| Evidence for answers | Must every derived answer cite evidence, or only some? |
| Stale-content handling | Hide, deprecate, or archive — chosen explicitly or assumed? |

For every **(X)** decision found:
1. If low impact (purely technical, easily changed): mark as an explicit assumption in Implementation Notes.
2. If it affects business behavior (e.g., who can override a reconciliation, what happens to published content when its source is retracted): **stop and ask** using `AskUserQuestion` before delivering the model.

Do not deliver the model until all material **(X)** decisions are either confirmed or documented as explicit assumptions.

---

## Output Format

```markdown
# Knowledge Management Archetype Model: [Domain Name]

## Domain Value
[What kind of knowledge is being managed, why it matters, and the canonical unit/representation if applicable]

## Concept Mapping

| Domain Concept | Knowledge Management Concept | Notes |
|----------------|------------------------------|-------|
| ... | ... | ... |

## Unmapped Concepts
[List or "None identified"]

## Knowledge Objects

| Object | Role | Description |
|--------|------|-------------|
| [name] | Knowledge Item / Source / Evidence / Assertion / Relation | [purpose] |

## Knowledge Operations

### [operation_name]
**Trigger**: [what causes this]
**Effect**: [what changes]
**Reversible**: Yes/No/Conditional

## Freshness & Review Rules

| Object | Review Cadence | Valid From | Valid To / Review Due | On Stale |
|--------|---------------|------------|------------------------|---------|
| [object] | [rule] | [rule] | [rule] | [hide / archive / warn / re-approve] |

## Confidence & Evidence Rules
- [rule]
- [rule]

## Scope & Access Rules
- [rule]
- [rule]

## Implementation Notes
[Key decisions, assumptions, edge cases, and any "It depends" outcomes]
```

---

## Common Patterns & Pitfalls

### Pattern: Truth Judgment Belongs to Stewards, Not the Model

Whether an assertion is *currently correct* is a judgment made by a human steward or an authoritative source — not something the archetype infers on its own. The model's job is to record what was asserted, by whom, with what evidence and confidence, and whether it passed review. It does not decide truth.

```
Application/steward layer:  "Is this assertion still correct?"
                            → steward checks evidence, cross-references sources, decides
                            → if confirmed: re-verify / re-approve in the model
                            → if contradicted: mark superseded, trigger revise/reconcile

Knowledge model:             records the assertion, its evidence, confidence, and review
                              state — enforces structural rules only (e.g., published items
                              must have passed review)
```

### Pattern: Freshness and Confidence Policy Is Computed Above the Model and Passed In

If the *behavior* of freshness or trust varies by context — e.g., how often content is reviewed, or how much weight a source gets — that variability does not belong inside the archetype itself.

Examples:
- "Legal-approved policies are reviewed every 90 days, community FAQ entries every 365 days" → the model receives `review_due` already computed; it does not contain the tiering logic.
- "Vendor documentation is trusted less than internally verified content" → the application assigns a `confidence` value before the model stores it; the model does not decide vendor trust.

**In the model**: document the *parameter* the archetype accepts (e.g., `review_due`, `confidence`, `publication_gate`) and note that its value is determined externally. Do not model the decision logic itself.

### Pattern: Do Not Let Document, Workflow, or Audit Concepts Leak In

- If a concept is really about *tracking edits over time* with no trust question attached, it belongs to a document/version model — reference it, don't reimplement it here.
- If a concept is really about *task state progression*, it belongs to a workflow/case model.
- If a concept is really about *proving what happened*, it belongs to an audit trail.

When in doubt, re-apply the fit test to the specific concept, not just the whole domain — a single domain can legitimately span multiple archetypes.

---

## Quality Checks

Before returning the model, verify:

- [ ] Fit test passed and documented
- [ ] Concept mapping table is present and complete
- [ ] Unmapped Concepts section is present (even if empty)
- [ ] Every knowledge object has an owner/steward, or an explicit "unowned" note
- [ ] Sources and evidence are modeled for every assertion-bearing object
- [ ] Freshness / review rules are defined for every knowledge object
- [ ] Confidence / trust rules are defined
- [ ] Scope / access rules are defined
- [ ] All clarifying question answers (or assumptions) are reflected in the model
- [ ] No silent business-policy assumptions
- [ ] No document/version, workflow, or audit-trail concepts leaked into this model

---

## Example

**Input:** "Support agents answer customer questions using product manuals and policy documents. Manuals are updated quarterly by the docs team. Policy documents need legal approval before publishing. Every answer must cite which manual section or policy it came from. If a manual and a policy disagree, policy wins."

**Output:**

```markdown
# Knowledge Management Archetype Model: Support Answer Knowledge Base

## Domain Value
SUPPORT_KNOWLEDGE — governed knowledge used to answer customer questions, sourced from
product manuals (docs team) and policy documents (legal-approved).

## Concept Mapping

| Domain Concept | Knowledge Management Concept | Notes |
|----------------|------------------------------|-------|
| Product manual | Source + Knowledge Item container | Owned by docs team |
| Policy document | Source + Knowledge Item container | Requires Review/Approval before Publication |
| Customer question | Trigger for Derived Answer | Not stored as knowledge itself |
| Cited section | Evidence | Attached to every Derived Answer |
| Manual vs. policy conflict | Relation (contradicts) + reconciliation rule | Policy wins — explicit priority |

## Unmapped Concepts
None identified.

## Knowledge Objects

| Object | Role | Description |
|--------|------|-------------|
| product_manual_kb | Knowledge Item container | Quarterly-updated manuals, docs team owned |
| policy_kb | Knowledge Item container | Legal-approved policies |
| manual_section | Evidence | Cited excerpt backing an assertion |

## Knowledge Operations

### revise
**Trigger**: Docs team publishes new manual version (quarterly)
**Effect**: New knowledge items created; superseded items marked deprecated
**Reversible**: No — creates new version, old one retained as superseded

### approve
**Trigger**: Legal review of a policy document completes
**Effect**: Policy item moves from in-review to published
**Reversible**: Conditional — legal can revoke approval

### reconcile conflicting sources
**Trigger**: Manual and policy assertions contradict on the same topic
**Effect**: Policy assertion takes precedence; manual assertion flagged as superseded for that topic
**Reversible**: N/A — reconciliation is a standing rule, not a one-off action

## Freshness & Review Rules

| Object | Review Cadence | Valid From | Valid To / Review Due | On Stale |
|--------|---------------|------------|------------------------|---------|
| product_manual_kb | Quarterly | On publish | Next quarterly release | Re-approve |
| policy_kb | To zależy / It depends — legal sets cadence per policy | On legal approval | `review_due` (policy-specific) | Warn, then archive if unreviewed |

## Confidence & Evidence Rules
- Every Derived Answer must cite at least one Evidence (manual section or policy clause).
- Policy assertions always outrank manual assertions on the same topic (explicit priority, not confidence score).

## Scope & Access Rules
- Both knowledge bases are scoped to support agents only; not customer-facing directly.

## Implementation Notes
- Policy review cadence varies per policy ("It depends") — modeled as a `review_due` parameter set externally by legal, not hardcoded.
- Reconciliation rule (policy beats manual) was explicitly stated in requirements — not assumed.
```
