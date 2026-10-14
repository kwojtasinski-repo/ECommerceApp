# Findings: mastra-book

Category: mastra-book (plan instance 4, output prefix `mastra-book`)
Source set: https://mastra.ai/books/patterns-of-building-ai-agents (1 URL; the book's chapters are not separately linked, see Gap 1)
Gathering date: 2026-10-14
Method: WebFetch with the planned extraction prompt, then raw HTML retrieved via `curl` (User-Agent Mozilla/5.0, HTTP 200, 455,674 bytes) to obtain verbatim quotes. WebFetch returns a model summary, not exact text, so every quote below was taken from the raw HTML.

## Chapter URL inventory

The page lists five chapters. No chapter has its own URL in the page HTML. The chapters are accordion items on the single book page, so no chapter has a URL of its own. Each chapter is listed below with the book URL and its accordion id.

| # | Chapter title (verbatim) | URL |
|---|---|---|
| 1 | 1. From Wishlist to Working Agent | https://mastra.ai/books/patterns-of-building-ai-agents (accordion `ch-1`, no separate URL) |
| 2 | 2. Intro to Context Engineering | https://mastra.ai/books/patterns-of-building-ai-agents (accordion `ch-2`, no separate URL) |
| 3 | 3. From MVP to Production | https://mastra.ai/books/patterns-of-building-ai-agents (accordion `ch-3`, no separate URL) |
| 4 | 4. Autonomy Is a Two-Edged Sword | https://mastra.ai/books/patterns-of-building-ai-agents (accordion `ch-4`, no separate URL) |
| 5 | 5. What's Next(ish) | https://mastra.ai/books/patterns-of-building-ai-agents (accordion `ch-5`, no sub-items) |

## Chapter section headings (table of contents, verbatim)

Source: https://mastra.ai/books/patterns-of-building-ai-agents, accordion payload in raw HTML.

### Chapter 1: From Wishlist to Working Agent
- Source: https://mastra.ai/books/patterns-of-building-ai-agents, raw HTML accordion item `ch-1`: `<ul class="mt-0!"><li>Whiteboard Agent Capabilities</li><li>Evolve Your Agent Architecture</li><li>Dynamic Agents</li><li>Human-in-the-Loop</li></ul>`
- Evidence: Section headings: "Whiteboard Agent Capabilities", "Evolve Your Agent Architecture", "Dynamic Agents", "Human-in-the-Loop".
- Confidence: High — literal table-of-contents text from the publisher's page. It shows section titles only, not section content.

### Chapter 2: Intro to Context Engineering
- Source: https://mastra.ai/books/patterns-of-building-ai-agents, accordion item `ch-2` (same page)
- Evidence: Section headings: "Parallelize Carefully", "Share Context Between Subagents", "Avoid Context Failure Modes", "Compress Context", "Feed Errors Into Context".
- Confidence: High — literal table-of-contents text. Section content not available (Gap 1).

### Chapter 3: From MVP to Production
- Source: https://mastra.ai/books/patterns-of-building-ai-agents, accordion item `ch-3` (same page)
- Evidence: Section headings: "List Failure Modes", "List Critical Business Metrics", "Cross-Reference Failure Modes and Success Metrics", "Iterate Against Your Evals", "Create an Eval Test Suite", "Have SMEs Label Data", "Create Datasets from Production Data", "Evaluate Production Data".
- Confidence: High — literal table-of-contents text. Section content not available (Gap 1).

### Chapter 4: Autonomy Is a Two-Edged Sword
- Source: https://mastra.ai/books/patterns-of-building-ai-agents (accordion item `ch-4`, same page)
- Evidence: Section headings: "Prevent the Lethal Trifecta", "Sandbox Code Execution", "Granular Agent Access Control", "Agent Guardrails".
- Confidence: High — literal table-of-contents text. Section content not available (Gap 1).

### Chapter 5: What's Next(ish)
- Source: https://mastra.ai/books/patterns-of-building-ai-agents (accordion item `ch-5`, same page)
- Evidence: The accordion item has no sub-section list in the payload (`"content":"$undefined"`).
- Confidence: High that no sub-sections are listed on the page. Content not available (Gap 1).

## Book-level description (publisher text on the page)

### Finding: book scope as described by the publisher
- Source: https://mastra.ai/books/patterns-of-building-ai-agents, raw HTML section "What's inside?"
- Evidence (verbatim): "Patterns of Building AI Agents covers practical patterns for taking AI agents from prototype to production. Learn strategies emerging from teams pushing agents into production at prominent AI companies. Chapter topics include human-in-the-loop patterns and dynamic agent architecture, context engineering techniques for parallelizing and compressing context, eval workflows for moving from MVP to production, and security patterns including guardrails, sandboxing and granular access controls."
- Confidence: Medium — this is marketing copy describing the book's topics. It shows what the publisher says the book covers, not what the chapters say. Not verified against chapter text (Gap 1).

### Finding: author and framing
- Source: https://mastra.ai/books/patterns-of-building-ai-agents, raw HTML intro paragraph
- Evidence (verbatim): "Written by Sam Bhagwat, CEO of Mastra, it covers design patterns, context engineering, eval workflows and security emerging from teams pushing agents into production at prominent AI companies."
- Confidence: Medium — publisher's self-description. Author identity is stated by the source; not independently verified (out of scope to check).

### Finding: publisher's FAQ on the book
- Source: https://mastra.ai/books/patterns-of-building-ai-agents, raw HTML FAQ (JSON-LD and visible text)
- Evidence (verbatim): "Patterns of Building AI Agents covers five chapters: From Wishlist to Working Agent, Intro to Context Engineering, From MVP to Production, Autonomy Is a Two-Edged Sword and What's Next(ish). Each chapter covers practical strategies and patterns emerging from teams pushing agents into production at prominent AI companies."
- Confidence: Medium — same publisher self-description as above, restating the chapter list.

### Finding: publisher's FAQ on the context engineering chapter
- Source: https://mastra.ai/books/patterns-of-building-ai-agents, raw HTML FAQ
- Evidence (verbatim): "The context engineering chapter in Patterns of Building AI Agents covers techniques including parallelizing carefully, sharing context between subagents, avoiding context failure modes, compressing context and feeding errors into context."
- Confidence: Medium — publisher summary; matches the chapter 2 table of contents.

### Finding: publisher's FAQ on the production chapter
- Source: https://mastra.ai/books/patterns-of-building-ai-agents, raw HTML FAQ
- Evidence (verbatim): "Patterns of Building AI Agents covers the practical steps for moving agents from MVP to production, including listing failure modes, defining critical business metrics, creating eval test suites, labeling data with subject matter experts and evaluating production data. It also covers security patterns for preventing the lethal trifecta, sandboxing code execution and granular agent access control"
- Confidence: Medium — publisher summary (FAQ answer), not chapter text.

## Gaps

### Gap 1: chapter body text not reachable (gated)
- URL: https://mastra.ai/books/patterns-of-building-ai-agents
- Symptom: The page is a sales/landing page. The HTTP response is 200, but the book's chapter text is not in the page. The page asks: "Enter your email to get your copy." The "Get your copy" control and the "Read" link lead to an email capture. Chapter sub-sections are listed as headings only, with no body text.
- Attempts:
  1. WebFetch with the planned extraction prompt. Returned the chapter list and the intro text only.
  2. `curl` with a browser User-Agent. Returned HTTP 200 with the same landing page. Searched the raw HTML for chapter-specific URLs (`patterns-of-building-ai-agents/...`), `books/` links, and body text under the chapter headings. None found.
  3. Not attempted: submitting the email capture (would hand over a user's email to an unrelated service; not done). Not attempted: guessing chapter URLs (not listed in the plan, and not confirmed to exist).
- Effect: No chapter-level patterns, concepts, or skills can be quoted from the book. Only the table of contents and the publisher's description are available. Any pattern-level content for this source is a gap.

### Gap 2: no per-chapter URLs exist in the page
- Symptom: The plan asked for each chapter URL to be listed. The page exposes no chapter links, and the accordion items have no `href`. The chapter rows above use the book URL.
- Effect: The plan's "fetch every chapter reachable from this page" step found nothing reachable beyond the landing page.

## Cross-links (recorded for the verification phase, not followed)

- https://mastra.ai/books/principles-of-building-ai-agents — a sibling book on the same page ("Principles" series entry, linked as "Start with the first book in the agentic series to learn the fundamentals"). Outside the 18 URLs, so not followed.
- A video ("Watch him go over a few chapters of his book", with a "Watch Introduction" item). Non-text media and not in the source list. Not fetched.
- The page footer links to https://mastra.ai/ai-agent-framework, https://mastra.ai/ai-agent-deployment, https://mastra.ai/ai-gateway and https://mastra.ai/studio. Product pages, outside the 18 URLs. Not followed.

## Rejected Information

| # | Information | Source | Rejection Reason | Re-include If |
|---|---|---|---|---|
| 1 | Sibling book "Principles of Building AI Agents" (linked from the same page) | https://mastra.ai/books/principles-of-building-ai-agents (link only, not fetched) | Outside the 18 listed URLs; the plan says not to follow links outside the list | The brief is widened to include the sibling book, or the verification phase needs it for a contradiction check |
| 2 | Embedded video of Sam Bhagwat presenting "a few chapters" | Link on https://mastra.ai/books/patterns-of-building-ai-agents | Non-text media; not a listed source; no transcript on the page | A transcript is available and the brief allows non-text sources |
| 3 | Mastra product pages (AI agent framework, deployment, gateway, studio) in the page footer and navigation | https://mastra.ai/ai-agent-framework, https://mastra.ai/ai-agent-deployment, https://mastra.ai/ai-gateway, https://mastra.ai/studio (links only) | Vendor product marketing, outside the 18 listed URLs | Brief allows vendor product pages as sources |
| 4 | Testimonial and social-media content on the book page (for example an X post about a TypeScript conference, and copy-count claims such as "32,500 copies have been distributed") | https://mastra.ai/books/patterns-of-building-ai-agents, raw HTML | Marketing and social proof; no pattern content; claims are unverified | Verification phase needs the distribution claim or the social posts for context |

## Summary of counts
- Chapters listed: 5 (all as headings; 0 with reachable body text)
- Chapter URLs with their own address: 0 (all point to the single book URL)
- Gaps: 2 (gated chapter content; no per-chapter URLs)
- Quotes from this source that carry pattern content: 0 (all quotes are table-of-contents text or publisher description, Medium or High, not chapter-level content)
