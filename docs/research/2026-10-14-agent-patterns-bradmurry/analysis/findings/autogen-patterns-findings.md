# Findings: autogen-patterns

Category: autogen-patterns (source category 5 of 5 in the research plan)
Sources: 2 URLs (AutoGen design-patterns intro, AutoGen multi-agent-debate)
Retrieval: 2026-10-14. WebFetch was run first; every quote below was re-checked against the raw HTML of the page (downloaded with curl, text extracted). Quotes are verbatim from the raw HTML. Some apostrophes in the debate page appear as replacement characters in the fetched copy; quotes here avoid them.
Site metadata on the debate and intro pages: `version = '0.16.0'`, `version_match = 'stable'`. This is the docs version the page itself declares; currency was not checked beyond that.

Confidence rule applied: single-source claims are capped at Medium (plan, Methodology).

---

## Intro page: design patterns overview

URL: https://microsoft.github.io/autogen/stable//user-guide/core-user-guide/design-patterns/intro.html

### F1. Definition of a multi-agent design pattern (Layer: Big Picture)
- Source: intro page, section "Intro". URL above.
- Evidence: "A multi-agent design pattern is a structure that emerges from message protocols: it describes how agents interact with each other to solve problems."
- Confidence: Medium. Explicit definitional quote from the page; single source.

### F2. Multi-agent vs single-agent claim (Layer: Big Picture)
- Source: intro page, section "Intro". URL above.
- Evidence: "Research works like AutoGen, MetaGPT and ChatDev have shown multi-agent systems out-performing single agent systems at complex tasks like software development."
- Confidence: Medium. The claim is attributed to cited research (AutoGen, MetaGPT, ChatDev). The page does not report figures, and the cited papers were not fetched (outside the 18-URL list), so the claim is attributed, not verified here.

### F3. ReAct as a tool-use pattern (Layer: Modeling)
- Source: intro page, section "Intro". URL above.
- Evidence: "the tool-equipped agent in the previous section employs a design pattern called ReAct, which involves an agent interacting with tools."
- Confidence: Medium. Explicit statement; single source. The "previous section" (tools page) was not fetched.

### F4. Two patterns announced for the following sections (Layer: Modeling)
- Source: intro page, section "Intro". URL above.
- Evidence: "In the next two sections, we will discuss two common design patterns: group chat for task decomposition, and reflection for robustness."
- Confidence: Medium. Announcement only. The content of those two sections is not on this page and was not fetched.

### F5. Pattern names listed in site navigation (Layer: Big Picture)
- Source: intro page sidebar navigation. URL above.
- Evidence: sidebar entries: "Code Execution", "Concurrent Agents", "Group Chat", "Handoffs", "Mixture of Agents", "Multi-Agent Debate", "Reflection", "Sequential Workflow".
- Confidence: Low. Navigation labels only. The page does not describe any of these patterns, and the linked pages were not fetched.

### F6. Implementation scope statement (Layer: Implementation)
- Source: intro page, section "Intro". URL above.
- Evidence: "You can implement any multi-agent design pattern using AutoGen agents."
- Confidence: Medium. Explicit statement of framework scope; single source.

---

## Multi-agent debate page: mechanics

URL: https://microsoft.github.io/autogen/stable//user-guide/core-user-guide/design-patterns/multi-agent-debate.html

### F7. Definition (Layer: Modeling)
- Source: debate page, section "Multi-Agent Debate". URL above.
- Evidence: "Multi-Agent Debate is a multi-agent design pattern that simulates a multi-turn interaction where in each turn, agents exchange their responses with each other, and refine their responses based on the responses from other agents."
- Confidence: Medium. Explicit definition; single source.

### F8. Scope of the example (Layer: Big Picture)
- Source: debate page, section "Multi-Agent Debate". URL above.
- Evidence: "This example shows an implementation of the multi-agent debate pattern for solving math problems from the GSM8K benchmark."
- Confidence: Medium. The page presents the example as a math-problem implementation. It does not claim generality beyond this example.

### F9. Agent roles (Layer: Modeling)
- Source: debate page, sections "Multi-Agent Debate", "Solver Agent", "Aggregator Agent". URL above.
- Evidence:
  - "There are of two types of agents in this pattern: solver agents and an aggregator agent." (verbatim, including the page's wording "There are of two")
  - "The solver agents are responsible for solving math problems and exchanging responses with each other."
  - "The aggregator agent is responsible for distributing math problems to the solver agents, waiting for their final responses, and aggregating the responses to get the final answer."
  - Code: `@default_subscription` `class MathSolver(RoutedAgent):` and `@default_subscription` `class MathAggregator(RoutedAgent):`
- Confidence: Medium. Explicit prose plus verbatim code; single source.

### F10. Interaction sequence (Layer: Modeling)
- Source: debate page, section "Multi-Agent Debate" (numbered list). URL above.
- Evidence (verbatim steps):
  1. "User sends a math problem to the aggregator agent."
  2. "The aggregator agent distributes the problem to the solver agents."
  3. "Each solver agent processes the problem, and publishes a response to its neighbors."
  4. "Each solver agent uses the responses from its neighbors to refine its response, and publishes a new response."
  5. "Repeat step 4 for a fixed number of rounds. In the final round, each solver agent publishes a final response."
  6. "The aggregator agent uses majority voting to aggregate the final responses from all solver agents to get a final answer, and publishes the answer."
- Confidence: Medium. Explicit ordered steps; single source. Step 5 gives no round count in prose.

### F11. Round control (Layer: Modeling)
- Source: debate page, sections "Solver Agent" and "Setting Up a Debate"; code in `MathSolver.handle_request`. URL above.
- Evidence:
  - Prose: "Repeat step 4 for a fixed number of rounds."
  - Constructor argument in every solver registration: `max_round=3`
  - Code comment: "# If the counter reaches the maximum round, publishes a final response."
  - Code: `self._round += 1` then `if self._round == self._max_round:` publishes `FinalSolverResponse`, else publishes `IntermediateSolverResponse`.
  - Derived reading (my reading of the code, not stated on the page): each solver makes 3 model calls (`_round` 1, 2, 3) and exchanges intermediate responses after the first and second calls. The sample log labels the calls "round 0", "round 1", "round 2". The page does not state this count explicitly.
- Confidence: Medium for the quoted facts (explicit text and code). The derived count is a Medium reading of code that the page does not spell out.

### F12. Aggregation by majority vote (Layer: Modeling)
- Source: debate page, section "Aggregator Agent"; code in `MathAggregator.handle_final_solver_response`. URL above.
- Evidence:
  - Prose: "The aggregator agent uses majority voting to aggregate the final responses from all solver agents to get a final answer, and publishes the answer."
  - Code: `majority_answer = max(set(answers), key=answers.count)`
  - Code: `if len(self._buffer) == self._num_solvers:` (the aggregator waits for exactly `num_solvers` final responses; no timeout is described)
- Confidence: Medium. Explicit prose and code; single source. Tie-breaking is not specified in prose or code (`max` over a `set` of answers), so it is a gap.

### F13. Topology: sparse ring of four solvers (Layer: Modeling)
- Source: debate page, sections "Multi-Agent Debate" and "Setting Up a Debate". URL above.
- Evidence:
  - "The solver agents are connected in a sparse manner following the technique described in Improving Multi-Agent Debate with Sparse Communication Topology."
  - "Each solver agent is connected to two other solver agents."
  - Figure text on the page:
    ```
    A --- B
    |     |
    |     |
    D --- C
    ```
- Confidence: Medium. Explicit statement and figure; single source. The cited paper is outside the list and was not followed (see Rejected Information).

### F14. Internal inconsistency in the page on solver adjacency (Layer: Modeling)
- Source: debate page, section "Setting Up a Debate" (prose, figure and code). URL above.
- Evidence:
  - Prose: "For example, agent A is connected to agents B and C."
  - Figure: A is adjacent to B and to D (the figure shows A---B and a vertical link from A to D).
  - Code (see F15): `TypeSubscription("MathSolverA", "MathSolverD")` and `TypeSubscription("MathSolverA", "MathSolverB")`, and the reverse subscriptions for D and B to A.
- Confidence: High that the page text is internally inconsistent (prose says A-C; figure and code say A-D and A-B). Which adjacency is intended is not resolved here. Running the code would settle it, which is out of scope.

### F15. Topology wiring in code (Layer: Implementation)
- Source: debate page, section "Setting Up a Debate". URL above.
- Evidence: Page comment: "Now we will create the solver agent topology using TypeSubscription, which maps each solver agent's publishing topic type to its neighbors' agent types."
  ```
  await runtime.add_subscription(TypeSubscription("MathSolverA", "MathSolverD"))
  await runtime.add_subscription(TypeSubscription("MathSolverA", "MathSolverB"))
  await runtime.add_subscription(TypeSubscription("MathSolverB", "MathSolverA"))
  await runtime.add_subscription(TypeSubscription("MathSolverB", "MathSolverC"))
  await runtime.add_subscription(TypeSubscription("MathSolverC", "MathSolverB"))
  await runtime.add_subscription(TypeSubscription("MathSolverC", "MathSolverD"))
  await runtime.add_subscription(TypeSubscription("MathSolverD", "MathSolverC"))
  await runtime.add_subscription(TypeSubscription("MathSolverD", "MathSolverA"))
  ```
- Confidence: Medium. Verbatim code; single source. See F14 for the mismatch with the prose.

### F16. Message protocol (Layer: Implementation)
- Source: debate page, section "Message Protocol". URL above.
- Evidence: Page prose: "IntermediateSolverResponse is the message exchanged among the solver agents in each round, and FinalSolverResponse is the message published by the solver agents in the final round."
  ```
  @dataclass class Question:           content: str
  @dataclass class Answer:             content: str
  @dataclass class SolverRequest:      content: str; question: str
  @dataclass class IntermediateSolverResponse: content: str; question: str; answer: str; round: int
  @dataclass class FinalSolverResponse: answer: str
  ```
- Confidence: Medium. Verbatim dataclass definitions; single source.

### F17. Topic structure (Layer: Implementation)
- Source: debate page, sections "Solver Agent" and "Aggregator Agent". URL above.
- Evidence:
  - "The solver agent is given a topic type, which is used to indicate the topic to which the agent should publish intermediate responses. This topic is subscribed to by its neighbors to receive responses from this agent."
  - "We use default_subscription() to let solver agents subscribe to the default topic, which is used by the aggregator agent to collect the final responses from the solver agents."
  - "The aggregator subscribes to the default topic using default_subscription(). The default topic is used to recieve user question, receive the final responses from the solver agents, and publish the final answer back to the user." (verbatim, including the page's spelling "recieve")
  - Code: `topic_id=DefaultTopicId(type=self._topic_type)` for `IntermediateSolverResponse`; `topic_id=DefaultTopicId()` for `FinalSolverResponse`, `SolverRequest`, `Question`, `Answer`.
- Confidence: Medium. Explicit prose and code; single source.

### F18. Per-round exchange and self-addressed refinement (Layer: Modeling)
- Source: debate page, section "Solver Agent"; code in `MathSolver.handle_response`. URL above.
- Evidence:
  - Code: `self._buffer.setdefault(message.round, []).append(message)`, then `if len(self._buffer[message.round]) == self._num_neighbors:`
  - Prompt text built from neighbor answers: "These are the solutions to the problem from other agents:" followed by "One agent solution: ..." lines, and the instruction "Using the solutions from other agents as additional information, can you provide your answer to the math problem?"
  - Code: `await self.send_message(SolverRequest(content=prompt, question=message.question), self.id)` (the solver sends the refinement request to itself).
  - Code: `self._buffer.pop(message.round)` (buffer cleared after each round).
- Confidence: Medium. Verbatim code and prompt text; single source.

### F19. Page guidance for isolating the debate in a larger application (Layer: Implementation)
- Source: debate page, section "Aggregator Agent". URL above.
- Evidence: "In a more complex application when you want to isolate the multi-agent debate into a sub-component, you should use type_subscription() to set a specific topic type for the aggregator-solver communication, and have the both the solver and aggregator publish and subscribe to that topic type."
- Confidence: Medium. This is the page's own guidance, recorded as a statement of the source; single source.

### F20. Runtime usage (Layer: Implementation)
- Source: debate page, sections "Setting Up a Debate" and "Solving Math Problems". URL above.
- Evidence:
  ```
  runtime = SingleThreadedAgentRuntime()
  model_client = OpenAIChatCompletionClient(model="gpt-4o-mini")
  await MathAggregator.register(runtime, "MathAggregator", lambda: MathAggregator(num_solvers=4))
  runtime.start()
  await runtime.publish_message(Question(content=question), DefaultTopicId())
  await runtime.stop_when_idle()
  await model_client.close()
  ```
  Page text: "We will be using the broadcast API, i.e., publish_message(), and we will be using topic and subscription to implement the communication topology."
- Confidence: Medium. Verbatim code; single source. Model name is the one the page uses (`gpt-4o-mini`).

### F21. Sample run output (Layer: Big Picture)
- Source: debate page, section "Solving Math Problems" (sample log). URL above.
- Evidence: Question: "Natalia sold clips to 48 of her friends in April, and then she sold half as many clips in May. How many clips did Natalia sell altogether in April and May?" Final log lines: "Aggregator MathAggregator:default received all final answers from 4 solvers." and "Aggregator MathAggregator:default publishes final answer:" followed by "72". The page's own summary of the run: all four solvers reached 72 and the aggregator returned 72.
- Confidence: Low for any general claim. This is one illustrative run with one math problem, and the page does not report accuracy, variance, or a comparison with single-agent output. It shows only that the mechanics run to completion in that case.

---

## Gaps

- No fetch failures. Both URLs returned content (HTTP success via curl; WebFetch also returned content).
- The aggregation tie-break rule is not specified in the page prose or code (F12).
- The number of rounds is not stated as a count in prose; the derived count in F11 is my code reading.
- The page's internal adjacency contradiction (F14) is recorded, not resolved. Resolution would need running the code, which is out of scope.
- No accuracy, benefit, or failure-rate data is given for debate versus single-agent output (F21).
- Linked pages not fetched (outside the 18-URL list, per the plan): see Rejected Information.

---

## Rejected Information

| # | Information | Source | Rejection Reason | Re-include If |
|---|---|---|---|---|
| 1 | "Improving Multi-Agent Debate with Sparse Communication Topology" (arXiv 2406.11776), cited as the basis for the sparse topology | debate page, "Multi-Agent Debate" section and link | Outside the 18-URL list; the plan says not to follow links outside the list | Added to the source list, or fetched during verification with user approval; it is the referenced basis for F13 |
| 2 | AutoGen paper (aka.ms/autogen-paper) | intro page, "Intro" section link | Outside the 18-URL list | Added to the source list or needed for verifying F2 |
| 3 | MetaGPT (arXiv 2308.00352) and ChatDev (arXiv 2307.07924) | intro page, "Intro" section links | Outside the 18-URL list; the claim they support (F2) is attributed, not verified here | Verification phase needs to check the F2 claim against the cited papers |
| 4 | Tools page, anchor `../components/tools.html#tool-equipped-agent` (the "previous section" that defines ReAct use) | intro page, "Intro" section link | Outside the 18-URL list | Verification of F3 is required, or the tools page is added to the list |
| 5 | Content of the "Group Chat" pattern (announced as "group chat for task decomposition") | intro page, "Intro" section and sidebar | Announced only; the linked page is outside the list | Group Chat page is added to the list |
| 6 | Content of the "Reflection" pattern (announced as "reflection for robustness") | intro page, "Intro" section and sidebar | Announced only; the linked page is outside the list | Reflection page is added to the list |
| 7 | Content of "Concurrent Agents", "Sequential Workflow", "Handoffs", "Mixture of Agents", "Code Execution" | intro page, sidebar and "Next page" link | Navigation labels only; linked pages outside the list | Any of these pages is added to the list; relevant to the pattern catalog deduplication |
| 8 | GSM8K benchmark (named as the example dataset) | debate page, "Multi-Agent Debate" section | Benchmark is not a source to gather from; not fetched | Benchmark-level characterization is needed for the debate evaluation step |
| 9 | Site navigation, header links, "Edit on GitHub", "Show Source", Discord, Twitter, PyPI, .NET docs, Studio, AgentChat, Extensions, API Reference, FAQ and Cookbook links | intro and debate pages | Site chrome, not pattern content | Not expected to be relevant |

Summary for this category: 21 findings (F1 to F21), 9 rejected items, 0 fetch failures.
