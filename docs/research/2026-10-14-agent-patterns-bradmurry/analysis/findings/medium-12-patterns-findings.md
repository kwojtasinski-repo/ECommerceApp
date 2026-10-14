# Findings: medium-12-patterns

Source under review: https://medium.com/@mohsho10/every-ai-agent-needs-these-12-design-patterns-or-it-will-eventually-fail-4970ebd4cb89

Status: GAP. No verbatim article text was retrieved, so no pattern could be extracted with a quote. The 12 patterns are not catalogued in this file.

## Gap: Medium article blocked (HTTP 403)
- Source: https://medium.com/@mohsho10/every-ai-agent-needs-these-12-design-patterns-or-it-will-eventually-fail-4970ebd4cb89
- Symptom: WebFetch returned `HTTP 403 Forbidden`, body not retrieved (tool message: "If this URL requires authentication, use an authenticated tool").
- Attempts:
  1. WebFetch, original URL, extraction prompt for full text and the 12 patterns. Result: HTTP 403.
  2. WebFetch, original URL, retry with a verbatim-text prompt (the one retry the plan allows). Result: HTTP 403.
  3. WebFetch via a third-party mirror (`https://freedium-mirror.cfd/https://medium.com/@mohsho10/...`). This was the one non-fabricating alternative the plan allows. The domain was not taken from the plan or a verified source; it was guessed for this attempt. The mirror responded, but it declined to return the article text and returned a model-written summary instead (see Rejected Information #1).
- Not attempted: the Medium "friend link" (the article page itself does not appear to have been retrieved, so no friend link was available), and any Wayback/cache copy (outside the plan's allowed fallbacks).
- Confidence: n/a (gap).
- Effect on the research: pattern names, quotes and failure modes for the 12 patterns are missing from the medium-12-patterns category. The verification phase needs one of: (a) a manual copy/paste of the article text into the workspace, (b) a working authenticated or cached route, or (c) a documented decision to drop this source.

## Rejected Information

| # | Information | Source | Rejection Reason | Re-include If |
|---|---|---|---|---|
| 1 | A 12-item list of pattern names with one-line descriptions, returned by the mirror as a model-written summary. The mirror stated it could not return the article verbatim. | https://freedium-mirror.cfd/https://medium.com/@mohsho10/every-ai-agent-needs-these-12-design-patterns-or-it-will-eventually-fail-4970ebd4cb89 (mirror response, not article text) | Not a verbatim quote of the source, so it cannot meet the "URL + quote" rule. It is a paraphrase from an unverified third-party domain with no way to check it against the article. Using it would mean reconstructing the source from a secondary summary, which the plan forbids. | The article text is obtained from the original URL or another verified route, so each pattern can be quoted directly. Or the user supplies the article text. |
| 2 | The Medium "friend link" and any cached/archived copy of the article. | Not retrieved | Not attempted. The plan allows one non-fabricating alternative, which was used in attempt 3. | The user approves an additional fallback route, or the verification phase requests it. |

Summary: 2 rejected items. Item 1 is a mirror paraphrase that was deliberately not used as a finding. Item 2 was not attempted.

## Cross-links
- None recorded. No article text was retrieved, so no outbound links were visible to the gatherer. The plan's rule that links outside the 18 URLs are not followed is unaffected.

## Actor Relevance
Omitted. The plan lists no actors.
