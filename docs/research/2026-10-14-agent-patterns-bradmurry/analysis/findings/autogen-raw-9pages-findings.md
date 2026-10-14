# Findings: autogen-raw-9pages

Category: autogen-raw-9pages (re-gather from full-text local extracts of nine AutoGen core-user-guide design-pattern pages).
Gathered: 2026-10-14. Method: full read of each local file; no web access used. No synthesis or recommendation.

URL base (every page): `https://microsoft.github.io/autogen/stable//user-guide/core-user-guide/design-patterns/<slug>.html`
Local files are under (lokalne kopie tekstu, poza repozytorium; źródła w SOURCES.md).

| # | File | Slug | Lines |
|---|---|---|---|
| 33 | [33 intro](https://microsoft.github.io/autogen/stable//user-guide/core-user-guide/design-patterns/intro.html) | intro | 25 |
| 34 | [34 concurrent-agents](https://microsoft.github.io/autogen/stable//user-guide/core-user-guide/design-patterns/concurrent-agents.html) | concurrent-agents | 288 |
| 35 | [35 sequential-workflow](https://microsoft.github.io/autogen/stable//user-guide/core-user-guide/design-patterns/sequential-workflow.html) | sequential-workflow | 281 |
| 36 | [36 group-chat](https://microsoft.github.io/autogen/stable//user-guide/core-user-guide/design-patterns/group-chat.html) | group-chat | 772 |
| 37 | [37 handoffs](https://microsoft.github.io/autogen/stable//user-guide/core-user-guide/design-patterns/handoffs.html) | handoffs | 650 |
| 38 | [38 mixture-of-agents](https://microsoft.github.io/autogen/stable//user-guide/core-user-guide/design-patterns/mixture-of-agents.html) | mixture-of-agents | 445 |
| 39 | [39 multi-agent-debate](https://microsoft.github.io/autogen/stable//user-guide/core-user-guide/design-patterns/multi-agent-debate.html) | multi-agent-debate | 489 |
| 40 | [40 reflection](https://microsoft.github.io/autogen/stable//user-guide/core-user-guide/design-patterns/reflection.html) | reflection | 401 |
| 41 | [41 code-execution-groupchat](https://microsoft.github.io/autogen/stable//user-guide/core-user-guide/design-patterns/code-execution-groupchat.html) | code-execution-groupchat | 263 |

Line numbers below refer to the local files. Layer tags: [BP] Big Picture, [MOD] Modeling, [IMPL] Implementation.
"Code-evidenced" = read from the code on the page, not stated in prose. Quotes are verbatim; code quotes are short single lines.

---

## PAGE 33: Intro

### 33-1 Definition of a multi-agent design pattern [BP][MOD]
- Source: https://microsoft.github.io/autogen/stable//user-guide/core-user-guide/design-patterns/intro.html, file [33 intro](https://microsoft.github.io/autogen/stable//user-guide/core-user-guide/design-patterns/intro.html):16-17
- Evidence: "A multi-agent design pattern is a structure that emerges from message protocols: it describes how agents interact with each other to solve problems."
- Confidence: High - explicit definition.

### 33-2 ReAct named as a pattern; claim about multi-agent vs single-agent [BP]
- Source: same page, file 33:10-13 and 33:18-20
- Evidence: "multi-agent systems out-performing single agent systems at complex tasks like software development." and "the tool-equipped agent in the previous section employs a design pattern called ReAct, which involves an agent interacting with tools."
- Confidence: High that the page says this. Medium as to truth: attributed to prior research (AutoGen, MetaGPT, ChatDev) with no citation or numbers on the page.

### 33-3 Pattern roster given by this page [BP]
- Source: same page, file 33:22-24
- Evidence: "we will discuss two common design patterns: group chat for task decomposition, and reflection for robustness."
- Confidence: High. Purposes assigned: group chat -> task decomposition; reflection -> robustness.
- Note: page has no code, no dataclasses, no topics, no termination or aggregation content, and no stated limitations.

---

## PAGE 34: Concurrent Agents

### 34-1 What the pattern is; three sub-patterns [BP][MOD]
- Source: https://microsoft.github.io/autogen/stable//user-guide/core-user-guide/design-patterns/concurrent-agents.html, file [34 concurrent-agents](https://microsoft.github.io/autogen/stable//user-guide/core-user-guide/design-patterns/concurrent-agents.html):9-18
- Evidence: "In this section, we explore the use of multiple agents working concurrently. We cover three main patterns:" then "Single Message & Multiple Processors", "Multiple Messages & Multiple Processors", "Direct Messaging".
- Confidence: High.

### 34-2 Message protocol (dataclasses) [MOD][IMPL]
- Source: file 34:42-49
- Evidence: `class Task:` with `task_id: str`; `class TaskResponse:` with `task_id: str` and `result: str` (both `@dataclass`).
- Confidence: High - code-evidenced.

### 34-3 Single message, multiple processors: fan-out by shared topic [MOD][IMPL]
- Source: file 34:57-59, 66-67, 84
- Evidence: "Each `Processor` agent subscribes to the default topic using the `default_subscription()` decorator." / "When publishing a message to the default topic, all registered agents will process the message independently." Code: `@default_subscription` on `class Processor(RoutedAgent)`; `await runtime.publish_message(Task(task_id="task-1"), topic_id=DefaultTopicId())`.
- Roles: two `Processor` instances registered as "agent_1" and "agent_2" (34:79-80).
- Control flow: no termination logic in the agents; run ends by `await runtime.stop_when_idle()` (34:86).
- Aggregation: none in this sub-pattern (agents only print).
- Confidence: High.

### 34-4 Multiple messages, multiple processors: routing by topic type [MOD][IMPL]
- Source: file 34:100-106, 112, 123
- Evidence: "`UrgentProcessor` subscribes to the \"urgent\" topic", "`NormalProcessor` subscribes to the \"normal\" topic", "We make an agent subscribe to a specific topic type using the `type_subscription()` decorator." Code: `@type_subscription(topic_type="urgent")`, `@type_subscription(topic_type="normal")`.
- Publishing: `TopicId(type="normal", source="default")` and `TopicId(type="urgent", source="default")` (34:146-147).
- Confidence: High.

### 34-5 Result collection via ClosureAgent and a results topic [MOD][IMPL]
- Source: file 34:163-165, 108-110, 175-181
- Evidence: "To collect these messages, we'll use a `ClosureAgent`." (apostrophe in source is a typographic one; quote trimmed to the verbatim tail) -> verbatim: "We've defined a dedicated topic `TASK_RESULTS_TOPIC_TYPE` where both `UrgentProcessor` and `NormalProcessor` publish their results." Code: `TASK_RESULTS_TOPIC_TYPE = "task-results"`; `subscriptions=lambda: [TypeSubscription(topic_type=TASK_RESULTS_TOPIC_TYPE, agent_type=CLOSURE_AGENT_TYPE)]`; collector does `await queue.put(message)`.
- Aggregation method: collected into an `asyncio.Queue[TaskResponse]` and drained by the caller; no combination or voting (34:168, 199-200).
- Observed output order differs from publish order: `TaskResponse(task_id='urgent-1', ...)` precedes `normal-1` (34:205-206). Code-evidenced; page does not comment.
- Confidence: High (quote 1 contains typographic apostrophes in the source, so only the unambiguous portion is relied on).

### 34-6 Direct messaging with request/response and fan-out/fan-in [MOD][IMPL]
- Source: file 34:212-226, 249-254
- Evidence: "Messages are addressed using the `AgentId`." / "The sender can expect to receive a response from the target agent." Code: `await asyncio.gather(self.send_message(subtask1, self.worker_instances[0]), self.send_message(subtask2, self.worker_instances[1]))`.
- Aggregation: DelegatorAgent concatenates string results: `combined_result = f"Part 1: {worker1_result.result}, " f"Part 2: {worker2_result.result}"` (34:253).
- Instance creation: "the runtime creates two instances of workers when sending those two messages." (34:226)
- Termination: request/response returns when both workers return; `stop_when_idle()` ends the runtime.
- Confidence: High.

### 34-7 Cross-reference [BP]
- Source: file 34:287
- Evidence: "check out the Mixture of Agents pattern, which relies heavily on concurrent agents."
- Confidence: High.

### 34-8 Limitations/caveats on the page
- None stated about concurrency, ordering, or failure handling. Only the Note that decorator-based subscription is one option and "the same agent class can be subscribed to different topics" via the alternative way (34:63).
- Confidence: High that none are stated.

### 34-9 Prose vs code contradictions
- None found. (Page naming: "Multiple messages & Multiple Processors" heading differs from list label "Multiple Messages & Multiple Processors" only by capitalization.)

---

## PAGE 35: Sequential Workflow

### 35-1 What the pattern is [BP][MOD]
- Source: https://microsoft.github.io/autogen/stable//user-guide/core-user-guide/design-patterns/sequential-workflow.html, file [35 sequential-workflow](https://microsoft.github.io/autogen/stable//user-guide/core-user-guide/design-patterns/sequential-workflow.html):9
- Evidence: "Sequential Workflow is a multi-agent design pattern where agents respond in a deterministic sequence."; "This pattern is useful for creating deterministic workflows where each agent contributes to a pre-specified sub-task."
- Confidence: High.

### 35-2 Roles/agents [MOD]
- Source: file 35:13-21
- Evidence: "The pipeline consists of four specialized agents:" - Concept Extractor Agent, Writer Agent, Format & Proof Agent, User Agent. Concept Extractor output: "a structured analysis in a single text block."; User Agent: "Presents the final, refined marketing copy to the user, completing the workflow."
- Confidence: High.

### 35-3 Message protocol [MOD][IMPL]
- Source: file 35:51-58
- Evidence: "The message protocol for this example workflow is a simple text message that agents will use to relay their work." Code: `class Message:` / `content: str` (`@dataclass`).
- Confidence: High. A single untyped-text message is used for all hops; no structured fields for status or approval.

### 35-4 Topics/subscriptions structure: one topic type per agent [MOD][IMPL]
- Source: file 35:29-32, 64-65, 68-71, 83
- Evidence: "agents communicate with each other by publishing their completed work as messages to the topic of the next agent in the sequence." / "The topic types are named after the agents in the sequence," Code: `concept_extractor_topic_type = "ConceptExtractorAgent"`, `writer_topic_type = "WriterAgent"`, `format_proof_topic_type = "FormatProofAgent"`, `user_topic_type = "User"`; `@type_subscription(topic_type=concept_extractor_topic_type)`.
- Sequence is hard-wired in each agent's code, e.g. `await self.publish_message(Message(response), topic_id=TopicId(writer_topic_type, source=self.id.key))` (35:108).
- Alternative subscription: "you can also use the `add_subscription()` method to subscribe to a topic through runtime directly." (35:78)
- Confidence: High.

### 35-5 Control flow and termination [MOD][IMPL]
- Source: file 35:218, 221-229, 174-175
- Evidence: "we can run the workflow by publishing a message to the first agent in the sequence." Code: `await runtime.stop_when_idle()`. Final agent only prints: "the user agent simply prints the final marketing copy to the console."
- Termination: no termination message, round limit, or branching; the chain ends when the last agent (UserAgent) publishes nothing further and the runtime goes idle. No loop or retry in the pattern. Code-evidenced.
- Confidence: High.

### 35-6 Aggregation [MOD]
- None: each stage passes its single text output to the next stage; no combining of results.
- Confidence: High (code-evidenced; page does not discuss aggregation).

### 35-7 Extensibility note [IMPL]
- Source: file 35:174-175
- Evidence: "In a real-world application, this could be replaced by storing the result to a database, sending an email, or any other desired action."
- Confidence: High.

### 35-8 Limitations/caveats and prose-vs-code
- No limitations stated on the page (no discussion of failure handling, validation between stages, or retries). Code: `assert isinstance(response, str)` in each handler (35:105, 137, 167) is the only check.
- Prose vs code contradictions: none found.

---

## PAGE 36: Group Chat

### 36-1 What the pattern is [BP][MOD]
- Source: https://microsoft.github.io/autogen/stable//user-guide/core-user-guide/design-patterns/group-chat.html, file [36 group-chat](https://microsoft.github.io/autogen/stable//user-guide/core-user-guide/design-patterns/group-chat.html):9-13, 25-28
- Evidence: "Group chat is a design pattern where a group of agents share a common thread of messages: they all subscribe and publish to the same topic." / "Group chat is useful for dynamically decomposing a complex task into smaller ones that can be handled by specialized agents with well-defined roles." / "It is also possible to nest group chats into a hierarchy with each participant a recursive group chat."
- Confidence: High.

### 36-2 Turn-taking and manager / speaker-selection mechanism [MOD]
- Source: file 36:17-23
- Evidence: "participants take turn to publish a message, and the process is sequential - only one agent is working at a time." (source uses a typographic dash; quote shortened below) -> verbatim: "the order of turns is maintained by a Group Chat Manager agent, which selects the next agent to speak upon receiving a message."
- Algorithm variability: "The exact algorithm for selecting the next agent can vary based on your application requirements. Typically, a round-robin algorithm or a selector with an LLM model is used."
- Confidence: High.

### 36-3 Message protocol (stated steps and classes) [MOD][IMPL]
- Source: file 36:89-97, 101-106
- Evidence: "The group chat manager selects the next speaker, sends out a `RequestToSpeak` message to that agent." / "The agent publishes a `GroupChatMessage` message to the common topic upon receiving the `RequestToSpeak` message." Code: `class GroupChatMessage(BaseModel): body: UserMessage`; `class RequestToSpeak(BaseModel): pass`.
- Note: these are pydantic `BaseModel`s, not `@dataclass`.
- Confidence: High.

### 36-4 Roles/agents [MOD]
- Source: file 36:9-15, 172, 181-182, 207-208, 261
- Evidence: Writer: `system_message="You are a Writer. You produce good work."` Editor: "Approve if the task is completed and the draft and illustration meets user's requirements." Illustrator uses a `generate_image` tool (DALL-E 3 via `FunctionTool`, `extra_create_args={"tool_choice": "required"}`). UserAgent: "The `UserAgent` implementation uses console input to get the user's input" (typographic apostrophe in source; claim relied on in shortened form). `GroupChatManager` is a separate LLM-based agent.
- Page intro lists "writer, illustrator, and editor" and "an agent to represent a human user" (36:12-15).
- Confidence: High.

### 36-5 Topics/subscriptions structure [MOD][IMPL]
- Source: file 36:391-393
- Evidence: "Each participant agent subscribes to both the group chat topic as well as its own topic in order to receive `RequestToSpeak` messages, while the group chat manager agent only subcribes to the group chat topic."
- Code: `group_chat_topic_type = "group_chat"`; per participant `TypeSubscription(topic_type=editor_topic_type, ...)` and `TypeSubscription(topic_type=group_chat_topic_type, ...)`. Manager publishes `RequestToSpeak()` to `DefaultTopicId(type=selected_topic_type)` (36:380).
- Run: initial `GroupChatMessage` published to `TopicId(type=group_chat_topic_type, source=session_id)` (36:487-495).
- Confidence: High.

### 36-6 LLM-based speaker selection (code-level) [IMPL]
- Source: file 36:300-302, 352-358, 373-382
- Evidence: "we also make sure the group chat manager always picks a different participant to speak next, by keeping track of the previous speaker." Prompt: "You are in a role play game. The following roles are available:" ... "Only return the role." Selection is by substring match: `if topic_type.lower() in completion.content.lower():`; failure: `raise ValueError(f"Invalid role selected: {completion.content}")`.
- Confidence: High - code-evidenced.

### 36-7 Termination / approval criterion [MOD][IMPL]
- Source: file 36:97, 291-293 (prose), 323-327 (code), 278
- Evidence (protocol): "This process continues until a termination condition is reached at the group chat manager, which then stops issuing `RequestToSpeak` message, and the group chat ends."
- Evidence (prose about manager): "The group chat manager checks if the editor has approved the draft by looking for the `\"APPORVED\"` keyword in the message."
- Evidence (code): `if message.body.source == "User":` ... `if message.body.content.lower().strip(string.punctuation).endswith("approve"):` -> `return`. UserAgent prompt: "Enter your message, type 'APPROVE' to conclude the task: "
- No maximum-round limit appears in the manager code. Code-evidenced.
- Confidence: High (see contradiction 36-C1).

### 36-8 Output of the run [IMPL]
- Source: file 36:760-761
- Evidence: "the writer, illustrator, and editor agents taking turns to speak and collaborate to generate a picture book, before asking for final approval from the user."
- Confidence: High.

### 36-9 Limitations and caveats stated on the page [BP]
- Source: file 36:38-44 and 36:763-772
- Evidence: "While this example illustrates the group chat mechanism, it is complex and represents a starting point from which you can build your own group chat system with custom agents and speaker selection algorithms."
- Evidence: "This example showcases a simple implementation of the group chat pattern" and "it is not meant to be used in real applications." (the intervening dash is typographic in the source).
- Evidence: "you can avoid using LLM when simple rules are sufficient and more reliable: you can use a rule that the editor always speaks after the writer."
- Pointer: "The AgentChat API has a built-in implementation of selector group chat." (36:43-44)
- Confidence: High.

### 36-C1 Contradiction: prose vs code on who approves and which keyword [MOD]
- Source: file 36:291-293 vs 323-327
- Prose says the manager looks for an editor approval keyword spelled `"APPORVED"`. Code checks only messages whose `source == "User"` and ends-with `"approve"`; no `APPORVED` string exists in the code (grep of file: only lines 291-293 mention it). Editor's message is never inspected for termination.
- Confidence: High - both quotes present, mismatch is direct.

### 36-C2 Minor: prose refers to steps "2 to 4 above" without numbered steps in the extract [MOD]
- Source: file 36:99 ("The following diagram illustrates steps 2 to 4 above.") while 36:91-97 are unnumbered paragraphs in the extracted text.
- Confidence: Medium - may be an extraction artifact (numbered list flattened); not verifiable from the local file.

---

## PAGE 37: Handoffs

### 37-1 What the pattern is and how a handoff is expressed [BP][MOD]
- Source: https://microsoft.github.io/autogen/stable//user-guide/core-user-guide/design-patterns/handoffs.html, file [37 handoffs](https://microsoft.github.io/autogen/stable//user-guide/core-user-guide/design-patterns/handoffs.html):9-10
- Evidence: "Handoff is a multi-agent design pattern introduced by OpenAI in an experimental project called Swarm. The key idea is to let agent delegate tasks to other agents using a special tool call."
- Confidence: High.

### 37-2 Handoff is expressed as a "delegate tool" returning a topic type [MOD][IMPL]
- Source: file 37:360-365 and 368-378
- Evidence: "special tools called delegate tools. The concept of delegate tool is only used in this design pattern". And: "when an AI agent calls a delegate tool, we transfer the task to another agent instead of continue generating responses using the model in the same agent." Code: `def transfer_to_sales_agent() -> str:` / `return sales_agent_topic_type`; also `transfer_to_issues_and_repairs`, `transfer_back_to_triage`, `escalate_to_human`.
- Confidence: High.

### 37-3 Handoff mechanics in the agent handler [IMPL]
- Source: file 37:114-121 and 173-193
- Evidence: "If the response contains a handoff tool call, the agent delegates the task to another agent by publishing a `UserTask` message to the topic specified in the tool call result." / "If the response is a regular tool call, the agent executes the tool and makes another call to the model to generate the next response, until the response is not a tool call."
- Code: the delegate context is the prior context plus the call and a result message `content=f"Transferred to {topic_type}. Adopt persona immediately."`; published via `await self.publish_message(task, topic_id=TopicId(topic_type, source=self.id.key))`; then "The task has been delegated, so we are done." (comment, `return`).
- Confidence: High.

### 37-4 Roles/agents [MOD]
- Source: file 37:35-45
- Evidence: "Triage Agent, responsible for understanding the customer's request and deciding which other agents to hand off to." (typographic apostrophe in source) ; "Human Agent, responsible for handling complex requests that the AI agents can't handle." Also a Refund Agent and a Sales Agent are named in prose; "the customer interacts with the chatbot through a User Agent."
- Delegate-tool wiring (code): Triage -> `[transfer_to_issues_and_repairs_tool, transfer_to_sales_agent_tool, escalate_to_human_tool]`; Sales and IssuesAndRepairs -> `[transfer_back_to_triage_tool]` (37:435-439, 467, 496).
- `escalate_to_human_tool` description: "Only call this if explicitly asked to." (37:390).
- Confidence: High.

### 37-5 Message protocol [MOD][IMPL]
- Source: file 37:85-89, 92-100
- Evidence: "`UserLogin` is a message published by the runtime when a user logs in and starts a new session." / "`UserTask` is a message containing the chat history of the user session. When an AI agent hands off a task to other agents, it also publishes a `UserTask` message." / "`AgentResponse` is a message published by the AI agents and the Human Agent, it also contains the chat history as well as a topic type for the customer to reply to."
- Code: `class UserTask(BaseModel): context: List[LLMMessage]`; `class AgentResponse(BaseModel): reply_to_topic_type: str; context: List[LLMMessage]`. (pydantic BaseModels, not dataclasses.)
- Confidence: High.

### 37-6 Topics/subscriptions and session scoping [MOD][IMPL]
- Source: file 37:350-354, 445, 538-541
- Evidence: topic types `"SalesAgent"`, `"IssuesAndRepairsAgent"`, `"TriageAgent"`, `"HumanAgent"`, `"User"`. Each agent subscribed only to its own topic: "it will receive messages published to its own topic only." (comment, 37:444).
- Session scoping: "`session_id` will be used to create all topic IDs in this user session and will also be used to create the agent ID for all the agents in this user session."
- Reply routing: user's next message goes to `TopicId(message.reply_to_topic_type, source=self.id.key)` (37:297-299), i.e. the last-responding agent.
- Confidence: High.

### 37-7 Control flow and termination [MOD][IMPL]
- Source: file 37:291-295, 554-555
- Evidence: `user_input = input("User (type 'exit' to close the session): ")` and `if user_input.strip().lower() == "exit":` -> prints "User session ended" and returns. Runtime: `await runtime.stop_when_idle()`.
- Termination is user-driven; no max-turn or loop limit in code. Code-evidenced.
- Human-in-the-loop: HumanAgent uses `input("Human agent input: ")`; "In a real-world application, you can improve this design as follows: ... send a notification via a chat application like Teams or Slack." (37:229-232)
- Confidence: High.

### 37-8 Stated advantages and note [BP]
- Source: file 37:13-19, 27-28
- Evidence: "It can scale to distributed environment by using distributed agent runtime." / "It affords the flexibility of bringing your own agent implementation." / "The natively async API makes it easy to integrate with UI and other systems." Note: "We are currently working on a high-level API for the handoff pattern in AgentChat so you can get started much more quickly."
- Confidence: High that the page claims this (claims about v0.4 vs OpenAI/v0.2 are the vendor's own, no evidence on the page: Medium on truth).

### 37-9 Observed run behaviour (code-evidenced output) [IMPL]
- Source: file 37:609-625
- Evidence: after a refund the user says "I want to talk to your manager"; the IssuesAndRepairsAgent replies "I can help with that, let me transfer you to a supervisor." and only after the user's "Okay" calls `transfer_back_to_triage`; Triage then calls `escalate_to_human`. Handoff of this case took two hops via triage, and the first agent's message precedes the actual tool call.
- Confidence: High that the log shows it; interpretation not offered by the page.

### 37-C1 Contradiction: prose agent names vs code [MOD]
- Source: file 37:39 vs 37:476-479 and 351
- Prose: "Refund Agent, responsible for processing refund requests." Code registers `IssuesAndRepairsAgent` (topic "IssuesAndRepairsAgent") and no "Refund Agent". The AIAgent docstring prose says "(i.e., Triage, Sales, and Issue and Repair Agents)" (37:107), which matches the code, so the page is internally inconsistent about the second agent.
- Confidence: High.

### 37-C2 Minor: prose "Triage Agent ... hand off to other agents" (human escalation) vs delegate-tool description [MOD]
- Not a contradiction in text: escalate tool is described "Only call this if explicitly asked to." while the HumanAgent is described as handling "complex requests that the AI agents can't handle." Two different triggers named; page does not reconcile them.
- Confidence: Medium (documenting a tension, not a verified error).

### 37-10 Limitations/caveats
- No explicit limitations section. Only the Note on the forthcoming AgentChat API, and the statement that the HumanAgent/UserAgent use console input as demo placeholders.

---

## PAGE 38: Mixture of Agents

### 38-1 What the pattern is [BP][MOD]
- Source: https://microsoft.github.io/autogen/stable//user-guide/core-user-guide/design-patterns/mixture-of-agents.html, file [38 mixture-of-agents](https://microsoft.github.io/autogen/stable//user-guide/core-user-guide/design-patterns/mixture-of-agents.html):9-15
- Evidence: "Mixture of Agents is a multi-agent design pattern that models after the feed-forward neural network architecture." / "The pattern consists of two types of agents: worker agents and a single orchestrator agent." / "Messages from the worker agents in a previous layer are concatenated and sent to all the worker agents in the next layer."
- Confidence: High.

### 38-2 Procedure / control flow [MOD]
- Source: file 38:22-30
- Evidence: "The orchestrator agent takes input a user task and first dispatches it to the worker agents in the first layer." ... "The process continues until the final layer is reached." ... "In the final layer, the orchestrator agent aggregates the results from previous layer and returns a single final result to the user."
- Termination: fixed number of layers (`num_layers`); no early stop. Code: `for i in range(self._num_layers - 1):` (38:128).
- Confidence: High.

### 38-3 Message protocol (dataclasses) [MOD][IMPL]
- Source: file 38:50-68
- Evidence: `WorkerTask(task: str, previous_results: List[str])`, `WorkerTaskResult(result: str)`, `UserTask(task: str)`, `FinalResult(result: str)` (all `@dataclass`).
- Confidence: High.

### 38-4 Communication API: direct messaging, not topics [MOD][IMPL]
- Source: file 38:32-33
- Evidence: "We use the direct messaging API `send_message()` to implement this pattern. This makes it easier to add more features like worker task cancellation and error handling in the future."
- Topics/subscriptions: none used. Workers are addressed by `AgentId(worker_type, f"{self.id.key}/layer_{i}/worker_{j}")` (38:131); fan-out via `asyncio.gather(...)` (38:136).
- Instances: "The instances of worker agents are automatically created when the orchestrator agent dispatches tasks to them." (38:170-171)
- Confidence: High.

### 38-5 Aggregation method [MOD][IMPL]
- Source: file 38:26, 87-90, 140-146
- Evidence: LLM-based synthesis, not voting. Prompt text: "Your task is to synthesize these responses into a single, high-quality response. It is crucial to critically evaluate the information provided in these responses, recognizing that some of it may be biased or incorrect."
- The same synthesize-prompt is used in two places: by workers when `previous_results` is non-empty (38:87-93) and by the orchestrator for the final aggregation (38:142-146). Previous results are numbered `f"{i+1}. {r}"` and concatenated.
- Confidence: High.

### 38-6 Configuration example [IMPL]
- Source: file 38:163-167, 182
- Evidence: "Let's set up the runtime with 3 layers of worker agents, each layer consisting of 3 worker agents." (typographic apostrophe in source; tail relied on) and "If you want to use different models, you will need to register multiple worker agent types, one for each model". Code: `worker_agent_types=["worker"] * 3, num_layers=3`.
- Confidence: High.

### 38-C1 Contradiction: "3 layers of worker agents" vs code dispatches 2 worker layers [MOD]
- Source: file 38:163 vs 38:128 and run log 38:200-294, 402-406
- Prose says 3 layers of worker agents. Code loops `range(self._num_layers - 1)`, i.e. 2 iterations for `num_layers=3`; the log shows "Dispatch to workers at layer 0" and "layer 1" only, then "Performing final aggregation" by the orchestrator's own model call. Effective structure: 2 worker layers + 1 orchestrator aggregation.
- Confidence: High - both facts directly quoted.

### 38-C2 Contradiction/looseness: who synthesizes between layers [MOD]
- Source: file 38:26 vs 38:87-93, 139
- Prose: "The orchestrator agent then synthesizes the results from the first layer and dispatches an updated task with the previous results to the worker agents in the second layer." Code: the orchestrator does not call the model between layers; it passes `previous_results=[r.result for r in results]` and each worker synthesizes them in its own call.
- Confidence: High.

### 38-C3 Minor: "publishes the final result" vs direct return [MOD]
- Source: file 38:107 ("the orchestrator agent aggregates the results and publishes the final result") vs 38:123 and 148 (`handle_task(...) -> FinalResult`, `return FinalResult(...)`) and 38:186 (`result = await runtime.send_message(UserTask(...), AgentId("orchestrator", "default"))`). The page states it uses direct messaging (38:32), so nothing is published to a topic.
- Confidence: High.

### 38-7 Limitations/caveats
- No explicit limitations stated. Only prose that direct messaging "makes it easier to add more features like worker task cancellation and error handling in the future" (i.e., not implemented). Observed run: all nine worker answers and the final answer are numerically identical (144/192/96); the page offers no evaluation of whether layers improved anything (suggests only "trying tasks from the International Mathematical Olympiad", 38:154).
- Confidence: High.

---

## PAGE 39: Multi-Agent Debate

### 39-1 What the pattern is [BP][MOD]
- Source: https://microsoft.github.io/autogen/stable//user-guide/core-user-guide/design-patterns/multi-agent-debate.html, file [39 multi-agent-debate](https://microsoft.github.io/autogen/stable//user-guide/core-user-guide/design-patterns/multi-agent-debate.html):9-11
- Evidence: "Multi-Agent Debate is a multi-agent design pattern that simulates a multi-turn interaction where in each turn, agents exchange their responses with each other, and refine their responses based on the responses from other agents."
- Task domain: "solving math problems from the GSM8K benchmark" (39:13-14).
- Confidence: High.

### 39-2 Roles [MOD]
- Source: file 39:16-21
- Evidence: "There are of two types of agents in this pattern: solver agents and an aggregator agent." / "The aggregator agent is responsible for distributing math problems to the solver agents, waiting for their final responses, and aggregating the responses to get the final answer."
- Solver count in the example: "4 solver agents and 1 aggregator agent" (39:252).
- Solver system prompt (code): "Limit your output within 100 words, and your final answer should be a single numerical number, in the form of {{answer}}, at the end of your response." All four solvers use the same prompt and model (`gpt-4o-mini`); no distinct personas, no critic role, no judge role.
- Confidence: High.

### 39-3 Stated procedure and number of rounds [MOD]
- Source: file 39:23-35
- Evidence: "Each solver agent processes the problem, and publishes a response to its neighbors." / "Each solver agent uses the responses from its neighbors to refine its response, and publishes a new response." / "Repeat step 4 for a fixed number of rounds. In the final round, each solver agent publishes a final response."
- Number of rounds in the example (code): `max_round=3` for each of the four solvers (39:281, 291, 301, 311). Counter logic: `self._round += 1` then `if self._round == self._max_round:` publish `FinalSolverResponse` (39:156-159). Run log shows solver outputs labelled "round 0", "round 1", "round 2" (39:371-481), i.e. three solver generations per solver, two neighbor-exchange steps before the final one.
- The page states only "a fixed number of rounds"; the value 3 is code-evidenced.
- Confidence: High.

### 39-4 Sparse topology [MOD][IMPL]
- Source: file 39:17-19, 253-265, 323-342
- Evidence: "The solver agents are connected in a sparse manner following the technique described in Improving Multi-Agent Debate with Sparse Communication Topology." and "Each solver agent is connected to two other solver agents."
- Diagram: `A --- B`, `|     |`, `D --- C` (a four-node ring).
- Implementation: each solver publishes intermediate responses to its own topic type (`topic_type="MathSolverA"` etc.), and neighbors subscribe via `TypeSubscription(<publisher topic>, <subscriber agent type>)`, e.g. `await runtime.add_subscription(TypeSubscription("MathSolverA", "MathSolverD"))` and `("MathSolverA", "MathSolverB")`.
- Quote on mechanism: "we will be using topic and subscription to implement the communication topology." (39:37-38)
- Confidence: High.

### 39-5 Message protocol (dataclasses) [MOD][IMPL]
- Source: file 39:66-70, 73-95
- Evidence: "`IntermediateSolverResponse` is the message exchanged among the solver agents in each round, and `FinalSolverResponse` is the message published by the solver agents in the final round." Dataclasses: `Question(content)`, `Answer(content)`, `SolverRequest(content, question)`, `IntermediateSolverResponse(content, question, answer, round)`, `FinalSolverResponse(answer)`.
- Confidence: High.

### 39-6 Topics and subscriptions structure [MOD][IMPL]
- Source: file 39:111-113, 204-207, 209-213, 343
- Evidence: "We use `default_subscription()` to let solver agents subscribe to the default topic, which is used by the aggregator agent to collect the final responses from the solver agents." and for the aggregator: "The default topic is used to recieve user question, receive the final responses from the solver agents, and publish the final answer back to the user."
- Stated guidance: "In a more complex application when you want to isolate the multi-agent debate into a sub-component, you should use `type_subscription()` to set a specific topic type for the aggregator-solver communication".
- Code: solvers publish `FinalSolverResponse` to `DefaultTopicId()` and intermediate responses to `DefaultTopicId(type=self._topic_type)` (39:159, 169). Comment: "# All solvers and the aggregator subscribe to the default topic."
- Round synchronization: a solver waits until `len(self._buffer[message.round]) == self._num_neighbors` before generating its next answer (39:177); it then sends `SolverRequest` to itself via `self.send_message(..., self.id)` (39:193).
- Confidence: High.

### 39-7 Aggregation: majority voting [MOD][IMPL]
- Source: file 39:35 and 239-243
- Evidence (prose): "The aggregator agent uses majority voting to aggregate the final responses from all solver agents to get a final answer, and publishes the answer."
- Evidence (code): `majority_answer = max(set(answers), key=answers.count)`; aggregation triggers when `len(self._buffer) == self._num_solvers` (39:237).
- Answers are extracted by regex from the `{{answer}}` form: `re.search(r"\{\{(\-?\d+(\.\d+)?)\}\}", model_result.content)` (39:151); no match raises `ValueError("The model response does not contain the answer.")` (39:153) - no recovery path shown.
- Confidence: High.

### 39-8 Tie-breaking and evaluation [MOD]
- Tie-breaking: the page states nothing about ties. Code-evidenced only: `max(set(answers), key=answers.count)` selects the maximum-count element; for a tie the result depends on iteration order of a Python `set` (implementation detail, not discussed on the page). With 4 solvers a 2-2 tie is arithmetically possible; the page does not address it.
- Evaluation: page mentions GSM8K as the problem source (39:13-14) but no accuracy measurement, ground-truth comparison, or comparison to a single agent is on the page. The single worked example (clips, answer 72) shows all 4 solvers agreeing in every round (39:371-486).
- Confidence: High that nothing on the page addresses these (full file read); the set-ordering remark describes code semantics, not a page claim.

### 39-9 Termination and control flow [MOD]
- Source: file 39:33, 156-159, 357-358, 483-486
- Evidence: termination = fixed round count per solver (`max_round`), then aggregator finishes after receiving all final answers; run ends by `await runtime.stop_when_idle()`. No consensus-based early exit, no convergence test.
- Confidence: High.

### 39-C1 Contradiction: prose vs diagram/code on A's neighbors [MOD]
- Source: file 39:264-265 vs diagram 39:256-262 and subscriptions 39:327-341
- Prose: "For example, agent A is connected to agents B and C." Diagram edges: A-B, B-C, C-D, D-A (so A is adjacent to B and D). Subscriptions: A's topic is consumed by D and B (`TypeSubscription("MathSolverA", "MathSolverD")` and `("MathSolverA", "MathSolverB")`); A consumes B's and D's topics. C is not connected to A.
- Confidence: High.

### 39-C2 Contradiction: how the debate is started [MOD]
- Source: file 39:349-351 vs code 39:356
- Prose: "We publish a `SolverRequest` to the default topic, and the aggregator agent will start the debate." Code: `await runtime.publish_message(Question(content=question), DefaultTopicId())`; the aggregator then publishes the `SolverRequest` (39:232). The earlier numbered procedure ("User sends a math problem to the aggregator agent", 39:25) matches the code.
- Confidence: High.

### 39-C3 Looseness: "fixed number of rounds" vs counting [MOD]
- Source: file 39:33 vs 39:137-159, 281
- "Repeat step 4 for a fixed number of rounds" - with `max_round=3` the final (third) generation is itself published as the final response, so there are two intermediate exchange steps (round 0 -> 1, 1 -> 2), not three. Whether "round" means generation or exchange is not defined on the page.
- Confidence: Medium - derived from code and log; page does not define "round".

### 39-10 Limitations/caveats stated on the page
- None explicit. Closest: "In a more complex application when you want to isolate the multi-agent debate into a sub-component, you should use `type_subscription()`..." (39:209-213) - guidance on topic isolation (the default topic is shared by everything).
- Code-level (not stated as caveats): a single aggregator buffer is cleared after each question (`self._buffer.clear()`, 39:245); per-solver `_history` accumulates; solver raises on a missing `{{answer}}`.
- Confidence: High that no limitation text exists.

---

## PAGE 40: Reflection

### 40-1 What the pattern is [BP][MOD]
- Source: https://microsoft.github.io/autogen/stable//user-guide/core-user-guide/design-patterns/reflection.html, file [40 reflection](https://microsoft.github.io/autogen/stable//user-guide/core-user-guide/design-patterns/reflection.html):9-12
- Evidence: "Reflection is a design pattern where an LLM generation is followed by a reflection, which in itself is another LLM generation conditioned on the output of the first one."
- Confidence: High.

### 40-2 Generator/critic loop and stated stopping condition [MOD]
- Source: file 40:14-18
- Evidence: "reflection can be implemented as a pair of agents, where the first agent generates a message and the second agent generates a response to the message. The two agents continue to interact until they reach a stopping condition, such as a maximum number of iterations or an approval from the second agent."
- Confidence: High.

### 40-3 Roles [MOD]
- Source: file 40:20-23, 106-108, 244-251
- Evidence: "There will be two agents: a coder agent and a reviewer agent". Coder system prompt: "Work with the reviewer to improve your code." with format "Thoughts: <Your comments>" / "Code: <Your code>". Reviewer system prompt: "You are a code reviewer. You focus on correctness, efficiency and safety of the code." JSON fields: correctness, efficiency, safety, approval ("<APPROVE or REVISE>"), suggested_changes.
- Confidence: High.

### 40-4 Message protocol (dataclasses) and flow [MOD][IMPL]
- Source: file 40:29-67
- Evidence: dataclasses `CodeWritingTask(task)`, `CodeWritingResult(task, code, review)`, `CodeReviewTask(session_id, code_writing_task, code_writing_scratchpad, code)`, `CodeReviewResult(review, session_id, approved: bool)`.
- Prose flow: "Depending on the `CodeReviewResult` message, if the code is approved, the coder agent sends a `CodeWritingResult` message back to the application, otherwise, the coder agent sends another `CodeReviewTask` message to the reviewer agent, and the process continues."
- Confidence: High.

### 40-5 Topics/subscriptions structure [MOD][IMPL]
- Source: file 40:92-95, 98, 236, 328-329
- Evidence: "We use the Broadcast API to implement the design pattern." / "The coder agent subscribes to the `CodeWritingTask` and `CodeReviewResult` messages, and publishes the `CodeReviewTask` and `CodeWritingResult` messages." / "The reviewer agent subscribes to the `CodeReviewTask` message and publishes the `CodeReviewResult` message."
- Both agents use `@default_subscription`: "Since all the agents are decorated with the `default_subscription()` class decorator, the agents when created will automatically subscribe to the default topic." Publishing: `TopicId("default", self.id.key)`.
- Confidence: High.

### 40-6 Approval criterion (code-level) [MOD][IMPL]
- Source: file 40:296, 162
- Evidence: `approved = review["approval"].lower().strip() == "approve"` and `if message.approved:` in the coder.
- Any reviewer output other than the exact string "approve" (case/whitespace-insensitive) yields `approved=False`.
- Reviewer is prompted to compare against earlier feedback: "If previous feedback was provided, see if it was addressed." (40:282)
- Confidence: High.

### 40-7 Session memory / state [IMPL]
- Source: file 40:226-227, 123, 256
- Evidence: "It stores message histories for different `CodeWritingTask` in a dictionary, so each task has its own history." Code: `self._session_memory: Dict[str, List[...]]` in both agents, keyed by `session_id = str(uuid.uuid4())` (40:128).
- Confidence: High.

### 40-8 Output formats [IMPL]
- Source: file 40:309-310, 291
- Evidence: "The `ReviewerAgent` uses JSON-mode when making an LLM inference request, and also uses chain-of-thought prompting in its system message." Code comment: "# TODO: use structured generation library e.g. guidance to ensure the response is in the expected format."
- Confidence: High.

### 40-9 Observed run [IMPL]
- Source: file 40:358-371
- Evidence: first review returned `approval: REVISE` (`'approved': False`), the coder revised, second review `approval: APPROVE` (`'approved': True`), then `CodeWritingResult` published. Two review cycles in the sample. The same log shows `Unhandled message: CodeWritingTask(...)` and `Unhandled message: CodeWritingResult(...)` (40:356, 371) because both agents share the default topic.
- Confidence: High.

### 40-C1 Contradiction: prose stopping condition vs code [MOD]
- Source: file 40:17-18 vs code 40:152-209
- Prose: stopping condition "such as a maximum number of iterations or an approval from the second agent." Code in `CoderAgent.handle_code_review_result` has only the `message.approved` branch; there is no iteration counter or maximum-iterations check anywhere in the file (the words "max" / "iteration" appear only in the prose). The loop continues for as long as the reviewer returns anything other than "approve".
- Confidence: High - read from code; absence verified by full read.

### 40-C2 Minor: protocol steps vs raise paths [MOD]
- Source: file 40:138-139, 197-198
- Prose protocol (40:59-67) has no failure path; code raises `ValueError("Code block not found.")` if the LLM response has no Markdown code block, and `json.loads(response.content)` at 40:293 has no error handling. Not stated as a limitation on the page.
- Confidence: Medium - code-evidenced, page silent.

### 40-10 Limitations/caveats stated on the page
- Only the TODO comment about structured generation (40:291). No limitations section.

---

## PAGE 41: Code Execution (slug: code-execution-groupchat)

### 41-1 What the pattern is [BP][MOD]
- Source: https://microsoft.github.io/autogen/stable//user-guide/core-user-guide/design-patterns/code-execution-groupchat.html, file [41 code-execution-groupchat](https://microsoft.github.io/autogen/stable//user-guide/core-user-guide/design-patterns/code-execution-groupchat.html):9-15
- Evidence: "In this section we explore creating custom agents to handle code generation and execution." / "We create two agent classes: `Assistant` and `Executor`. The `Assistant` agent writes code and the `Executor` agent executes the code."
- Provided alternatives: "`AssistantAgent()`, `CodeExecutorAgent()`" (41:9).
- Confidence: High.

### 41-2 Message protocol [MOD][IMPL]
- Source: file 41:15-16, 37-39
- Evidence: "We also create a `Message` data class, which defines the messages that are passed between the agents." Code: `@dataclass class Message: content: str`.
- Confidence: High.

### 41-3 Topics/subscriptions [MOD][IMPL]
- Source: file 41:41, 71, 59, 85
- Evidence: both agents use `@default_subscription`; Assistant publishes `Message(content=result.content)` to `DefaultTopicId()`; Executor publishes `Message(content=result.output)` to `DefaultTopicId()`. Initial message: `Message("Create a plot of NVIDA vs TSLA stock returns YTD from 2024-01-01.")` (sic) published to the default topic (41:131-133).
- Note: despite the "groupchat" slug, the page has no group chat manager, no `RequestToSpeak`, and no speaker selection; two agents share one default topic.
- Confidence: High.

### 41-4 Control flow and termination [MOD][IMPL]
- Source: file 41:77-85, 136
- Evidence (code): `code_blocks = extract_markdown_code_blocks(message.content)`; `if code_blocks:` execute and publish result. `await runtime.stop_when_idle()`.
- Termination is not described in prose. Code-evidenced: Executor publishes only when the Assistant's message contains a Markdown code block, so the exchange ends when an Assistant reply contains none. The Assistant, by contrast, replies to every `Message` it receives (including its own published messages not handled? - handler is for `Message`; page does not discuss self-delivery).
- No max-iterations or explicit approval. Observed: two Assistant/Executor cycles, final Assistant text states "It looks like the data fetching process completed successfully." (41:244).
- Confidence: High for the code facts; Medium for the termination reading (inferred, page silent).

### 41-5 Decoupling statement [BP]
- Source: file 41:89-93
- Evidence: "the agents' logic, whether it is using model or code executor, is completely decoupled from how messages are delivered. This is the core idea: the framework provides a communication infrastructure, and the agents are responsible for their own logic." (apostrophe in source is typographic; the second sentence is verbatim)
- Confidence: High.

### 41-6 Safety caveat [BP][IMPL]
- Source: file 41:18-20
- Evidence: "Code generated in this example is run within a Docker container." and "Local code execution is available (`LocalCommandLineCodeExecutor`) but is not recommended due to the risk of running LLM generated code in your local environment."
- Confidence: High.

### 41-7 Observed failure and recovery [IMPL]
- Source: file 41:183-186, 190, 240
- Evidence: first Executor output "ModuleNotFoundError: No module named 'pandas'"; the Assistant replied that it "can't install packages or check the environment directly"; the second Executor output shows the download completed ("2 of 2 completed") with no installation step visible in the log. The page does not explain how the environment changed between the two runs.
- Confidence: High that the log shows this; cause not stated.

### 41-8 Distributed runtime mention [BP]
- Source: file 41:258-259
- Evidence: "AutoGen also supports a distributed agent runtime, which can host agents running on different processes or machines, with different identities, languages and dependencies."
- Confidence: High (vendor claim).

### 41-9 Prose-vs-code contradictions
- None strictly. Page title "Code Execution" vs slug `code-execution-groupchat` is a naming mismatch only (see 41-3).

---

## Cross-page structural comparison (facts only, no recommendation)

| Page | Comm API | Topics used | Termination | Aggregation | Explicit limits on page |
|---|---|---|---|---|---|
| 34 concurrent | publish and send_message | default, type topics, results topic | `stop_when_idle` | queue collect / string concat | none |
| 35 sequential | publish | one type topic per agent | `stop_when_idle` after last agent | none | none |
| 36 group chat | publish | shared `group_chat` + per-agent topic | manager stops issuing `RequestToSpeak` on user "approve" (code) | none | "not meant to be used in real applications" |
| 37 handoffs | publish | per-agent topic | user types "exit" | none | note: high-level API in progress |
| 38 mixture | send_message | none | fixed layers | LLM synthesis | none |
| 39 debate | publish + self send_message | per-solver topic types, default topic | fixed `max_round` | majority vote | none |
| 40 reflection | publish | default topic | reviewer approval (no max iterations in code) | none | TODO about structured output |
| 41 code exec | publish | default topic | no code block in reply (inferred) | none | Docker caution |
| 33 intro | n/a | n/a | n/a | n/a | n/a |

- Source: derived from the quoted sections above (this table restates findings; no new claims).
- Confidence: High where each cell has a citation above; "inferred" noted.

## Cross-links recorded for verification phase
- 34 -> Mixture of Agents (34:287).
- 36 -> AgentChat selector group chat (36:43-44, 36:770-772).
- 37 -> OpenAI Swarm origin (37:9); AgentChat handoff API in development (37:27-28).
- 39 -> "Improving Multi-Agent Debate with Sparse Communication Topology" (39:17-18, link text only; not followed).
- 33 -> AutoGen, MetaGPT, ChatDev research mentions (33:10-13).

## Declarative Conclusions
Not applicable: external documentation only; no local human sources.

## Rejected Information

| # | Information | Source | Rejection Reason | Re-include If |
|---|---|---|---|---|
| 1 | Full LLM-generated outputs (story text, marketing copy, cookie math, stock code, GSM8K solver transcripts, Gingerbread illustrations) | files 35, 36, 38, 39, 41 output blocks | Demo output, no pattern-mechanics content beyond the facts already extracted | A downstream step needs example transcripts or quality assessment |
| 2 | Import lists and boilerplate (`rich` display setup, `pip install rich`, logging setup) | files 36, 40 | Implementation scaffolding unrelated to pattern semantics | Implementation-level porting is in scope |
| 3 | Tool implementations `execute_order`, `look_up_item`, `execute_refund`, DALL-E `_image_gen` | files 37, 36 | Demo domain functions; handoff/tool-call mechanics extracted instead | Tool design is in scope |
| 4 | Concurrency timing output (`asyncio.sleep` demos) | file 34 | Simulated work, no design claim | Performance behaviour is in scope |
| 5 | Mid-run text of the Gingerbread story (lines 550-750 of file 36) | file 36 | Generated content only; first and last portions read and the approval/termination output confirmed | The manager's sequence of selections needs to be traced from the log |

## Actor Relevance
None - research is actor-agnostic (per research-plan.md).
