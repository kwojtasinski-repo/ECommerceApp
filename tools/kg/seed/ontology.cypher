// =============================================================================
// ECommerceApp — Knowledge Graph Ontology Layer
// Source: ontology.json (machine-readable twin, read by the validator)
//
// Layer model (see tools/kg-simple-demo/ARCHITECTURE.md in architekt-jutra-code
// for the full stack-agnostic rationale — this file applies it to ECommerceApp)
// -----------------------------------------------------------------------------
//   Layer 0 — Spine:    System, Host                    (hand-authored, static)
//   Layer 1 — Modules:  Module                           (hand-listed, machine-populated)
//   Layer 2 — Artifacts: Entity, Repository, Action,
//                        Endpoint, Page, ScriptModule    (100% machine-generated)
//   Layer 3 — Variability point (this system's integration mechanism):
//                        Message, MessageHandler,
//                        Query, QueryHandler, Job
//   Layer 4 — Cross-cutting: Role, Policy                (machine-derived from
//                        [Authorize] attributes; Feature/event-storming layer
//                        deliberately NOT modeled here — human-curated only,
//                        add later via overrides if needed)
//
// Same tagging convention as the AJ demo: every node here is also tagged
// :Ontology. Instance nodes (real Modules, real Messages, ...) never wear
// :Ontology, so the two layers never overlap.
//
//   :Ontology:EntityType    — concept (Module, Job, Message, ...)
//   :Ontology:Property      — typed attribute owned by an EntityType
//   :Ontology:RelationType  — allowed relation kind (CONTAINS, HANDLED_BY, ...)
//   :Ontology:RelationRule  — reified (source, RelationType, target) triple
//   :Ontology:OntologyDoc   — top-level metadata
//
// Idempotent: re-running is safe (MERGE on stable keys).
// =============================================================================

// -----------------------------------------------------------------------------
// 0. CONSTRAINTS
// -----------------------------------------------------------------------------
CREATE CONSTRAINT ontology_entity_type_name   IF NOT EXISTS FOR (n:EntityType)   REQUIRE n.name IS UNIQUE;
CREATE CONSTRAINT ontology_relation_type_name IF NOT EXISTS FOR (n:RelationType) REQUIRE n.name IS UNIQUE;
CREATE CONSTRAINT ontology_property_id        IF NOT EXISTS FOR (n:Property)     REQUIRE n.id   IS UNIQUE;
CREATE CONSTRAINT ontology_relation_rule_id   IF NOT EXISTS FOR (n:RelationRule) REQUIRE n.id   IS UNIQUE;
CREATE CONSTRAINT ontology_doc_id             IF NOT EXISTS FOR (n:OntologyDoc)  REQUIRE n.id   IS UNIQUE;

// -----------------------------------------------------------------------------
// 1. ONTOLOGY METADATA
// -----------------------------------------------------------------------------
MERGE (doc:Ontology:OntologyDoc {id: 'ecommerceapp-kg-ontology'})
SET doc.title = 'ECommerceApp — Graf wiedzy',
    doc.description = 'Code-derived structural knowledge graph of ECommerceApp — a .NET layered monolith (API/Application/Domain/Infrastructure) organized internally by bounded-context folders, with two front-doors (API controllers, Web MVC/Razor) sharing the same Application-layer Actions, and a homegrown Outbox/Inbox message broker (async pub/sub) plus a synchronous in-process query channel (ModuleClient) as the cross-module integration mechanism. Answers structural questions for architects: which module owns what, who publishes/consumes which event, who can call what (RBAC), and how one module actually depends on another — traced from real code, not from docs that may have drifted.';

// =============================================================================
// 2. ENTITY TYPES (concepts)
// =============================================================================

// --- Layer 0/1 ---
MERGE (et:Ontology:EntityType {name: 'System'})
SET et.label = 'System', et.layer = 0,
    et.description = 'Top-level deployable: ECommerceApp as a whole.';

MERGE (et:Ontology:EntityType {name: 'Host'})
SET et.label = 'Host', et.layer = 0,
    et.description = 'A deployable front-door process. Two instances: ApiHost (ECommerceApp.API, JSON REST) and WebHost (ECommerceApp.Web, server-rendered MVC/Razor with Areas). Both reference the same Application layer in-process — they are not separate services calling each other over HTTP.';

MERGE (et:Ontology:EntityType {name: 'Module'})
SET et.label = 'Module', et.layer = 1,
    et.description = 'A bounded context: a folder present under Domain/Application/Infrastructure (and usually its own DbContext + EF configurations). NOT the same as a Host — a Module is shared business logic that both ApiHost and WebHost can expose. Identity is the leaf domain folder when siblings exist with independent identity (Sales/{Orders,Payments,Coupons,Fulfillment}), or the parent folder when it has exactly one meaningful leaf (Catalog, Inventory, Presale->Checkout). Backoffice is a Module by convention even though it owns no entities of its own — it is an intentional cross-module admin facade.';

// --- Layer 2 ---
MERGE (et:Ontology:EntityType {name: 'Entity'})
SET et.label = 'Entity', et.layer = 2,
    et.description = 'Domain-owned persisted type. Marker: a class configured by an Infrastructure/<Module>/Configurations/*Configuration.cs implementing IEntityTypeConfiguration<T> — NOT [Table] attributes (Domain layer is EF-agnostic by design). The table name comes from that same file''s builder.ToTable(...) call, not from convention-guessing.';

MERGE (et:Ontology:EntityType {name: 'Repository'})
SET et.label = 'Repository', et.layer = 2,
    et.description = 'Persistence component for an Entity. Marker: interface I*Repository (Application) with an implementation under Infrastructure/<Module>/Repositories.';

MERGE (et:Ontology:EntityType {name: 'Action'})
SET et.label = 'Action', et.layer = 2,
    et.description = 'A business operation: a public method on an Application-layer *Service class. Shared by both Hosts — the same Action can be EXPOSED_BY an API Endpoint and a Web Page simultaneously (Web controllers inject Application services directly, not via HTTP). query/command classification is NOT modeled yet — .NET has no direct equivalent of the readOnly-transaction marker used elsewhere; revisit once a real convention is chosen.';

MERGE (et:Ontology:EntityType {name: 'Endpoint'})
SET et.label = 'Endpoint', et.layer = 2,
    et.description = 'HTTP-facing entry point on ApiHost: an [HttpGet]/[HttpPost]/... method on an [ApiController] under API/Controllers/<Module>/*Controller.cs. No fixed URI-path convention encodes the module — module assignment comes from the controller''s folder.';

MERGE (et:Ontology:EntityType {name: 'Page'})
SET et.label = 'Page', et.layer = 2,
    et.description = 'Server-rendered entry point on WebHost: an MVC action method + Razor view under Web/Areas/<Area>/Controllers or Web/Controllers. Distinct from Endpoint (no HTTP verb/JSON contract — renders a View).';

MERGE (et:Ontology:EntityType {name: 'ScriptModule'})
SET et.label = 'ScriptModule', et.layer = 2,
    et.description = 'Client-side JS module loaded via RequireJS/AMD (define([...], fn) / require([...], fn)) under Web/wwwroot/js. CONVENTION-DEPENDENT — if the frontend ever migrates off AMD to ES modules/a bundler, this parser will silently find zero ScriptModule nodes instead of erroring (no ontology triple becomes invalid, the extraction rule just stops matching). The codegen MUST warn if a previously-nonzero-yield parser returns zero nodes — do not treat silence as success.';

// --- Layer 3: integration mechanism (async pub/sub + sync request/response) ---
MERGE (et:Ontology:EntityType {name: 'Message'})
SET et.label = 'Message', et.layer = 3, et.secondaryLabel = 'ModuleContract',
    et.description = 'Async, fire-and-forget cross-module event. Marker: record implementing IMessage, registered with an explicit string key in MessageTypeRegistry (deliberately not reflection-scanned). Delivered via the Outbox/Inbox pipeline — 0..N handlers, eventual consistency, idempotency-guarded on the consumer side. Also tagged :ModuleContract (shared with Query) so both integration channels can be queried together when the distinction between sync/async does not matter.';

MERGE (et:Ontology:EntityType {name: 'MessageHandler'})
SET et.label = 'MessageHandler', et.layer = 3,
    et.description = 'Consumer of a Message. Marker: class implementing IMessageHandler<T> or IIdAwareMessageHandler<T> (the latter = idempotency-aware, tracked via the Inbox/ProcessedMessage guard).';

MERGE (et:Ontology:EntityType {name: 'Query'})
SET et.label = 'Query', et.layer = 3, et.secondaryLabel = 'ModuleContract',
    et.description = 'Sync, blocking cross-module request. Marker: type implementing IQuery<TResult>, dispatched via ModuleClient.SendAsync. Exactly one handler, immediate response — the opposite delivery guarantee from Message. Also tagged :ModuleContract.';

MERGE (et:Ontology:EntityType {name: 'QueryHandler'})
SET et.label = 'QueryHandler', et.layer = 3,
    et.description = 'Handler of a Query. Marker: class implementing IQueryHandler<TQuery,TResult>.';

MERGE (et:Ontology:EntityType {name: 'Job'})
SET et.label = 'Job', et.layer = 3,
    et.description = 'Background unit of work. Marker: class implementing IScheduledTask, dispatched centrally by JobDispatcherService regardless of trigger source. Three trigger modes exist (JobTriggerSource enum): Scheduled (recurring, backed by a ScheduledJob DB row with a Cronos cron expression — the cron STRING ITSELF IS RUNTIME DATA, not statically extractable from source), Deferred (per-entity, one-shot, created dynamically via IDeferredJobScheduler.ScheduleAsync — the caller is a real, findable Action or MessageHandler), Manual (any registered Job can additionally be triggered by an Administrator via the Backoffice Jobs UI or POST /api/jobs/register — a platform-wide capability, not a per-job fact worth its own edge).';

// --- Layer 4: RBAC ---
MERGE (et:Ontology:EntityType {name: 'Role'})
SET et.label = 'Role', et.layer = 4,
    et.description = 'An atomic [Authorize(Roles=...)] role string (e.g. Administrator, Manager, Service). IMPORTANT: some C# constants (MaintenanceRole, ManagingRole) are comma-joined ALIASES for several real roles — the parser MUST split those before emitting Role nodes/edges, never emit a node for the alias constant itself.';

MERGE (et:Ontology:EntityType {name: 'Policy'})
SET et.label = 'Policy', et.layer = 4,
    et.description = 'An [Authorize(Policy=...)] named authorization policy (e.g. TrustedApiUser) — a second, independent RBAC mechanism alongside Role-based checks.';

// =============================================================================
// 3. PROPERTIES (typed attributes per EntityType)
// =============================================================================
MERGE (p:Ontology:Property {id: 'Module.name'})    SET p.name='name',    p.kind='string';
MERGE (p:Ontology:Property {id: 'Module.path'})     SET p.name='path',    p.kind='string', p.description='Repo-relative folder(s) this module maps to.';
MATCH (et:EntityType {name:'Module'}), (p:Property) WHERE p.id STARTS WITH 'Module.' MERGE (et)-[:HAS_PROPERTY]->(p);

MERGE (p:Ontology:Property {id: 'Entity.name'})  SET p.name='name',  p.kind='string';
MERGE (p:Ontology:Property {id: 'Entity.fqcn'})  SET p.name='fqcn',  p.kind='string';
MERGE (p:Ontology:Property {id: 'Entity.table'}) SET p.name='table', p.kind='string', p.description='From IEntityTypeConfiguration<T>.Configure -> builder.ToTable(...).';
MATCH (et:EntityType {name:'Entity'}), (p:Property) WHERE p.id STARTS WITH 'Entity.' MERGE (et)-[:HAS_PROPERTY]->(p);

MERGE (p:Ontology:Property {id: 'Endpoint.method'})  SET p.name='method',  p.kind='enum', p.values=['GET','POST','PUT','PATCH','DELETE'];
MERGE (p:Ontology:Property {id: 'Endpoint.uri'})     SET p.name='uri',     p.kind='string';
MERGE (p:Ontology:Property {id: 'Endpoint.handler'}) SET p.name='handler', p.kind='string';
MATCH (et:EntityType {name:'Endpoint'}), (p:Property) WHERE p.id STARTS WITH 'Endpoint.' MERGE (et)-[:HAS_PROPERTY]->(p);

MERGE (p:Ontology:Property {id: 'Page.area'})       SET p.name='area',       p.kind='string';
MERGE (p:Ontology:Property {id: 'Page.controller'}) SET p.name='controller', p.kind='string';
MERGE (p:Ontology:Property {id: 'Page.action'})     SET p.name='action',     p.kind='string';
MERGE (p:Ontology:Property {id: 'Page.route'})      SET p.name='route',     p.kind='string';
MATCH (et:EntityType {name:'Page'}), (p:Property) WHERE p.id STARTS WITH 'Page.' MERGE (et)-[:HAS_PROPERTY]->(p);

MERGE (p:Ontology:Property {id: 'Message.name'})           SET p.name='name',           p.kind='string';
MERGE (p:Ontology:Property {id: 'Message.messageTypeKey'}) SET p.name='messageTypeKey', p.kind='string', p.description='The MessageTypeRegistry key, e.g. "orders.order.placed".';
MERGE (p:Ontology:Property {id: 'Message.fields'})         SET p.name='fields',         p.kind='string[]', p.description='Record positional properties as "Name:Type", parsed directly from the C# record declaration.';
MATCH (et:EntityType {name:'Message'}), (p:Property) WHERE p.id STARTS WITH 'Message.' MERGE (et)-[:HAS_PROPERTY]->(p);

MERGE (p:Ontology:Property {id: 'MessageHandler.name'})    SET p.name='name',    p.kind='string';
MERGE (p:Ontology:Property {id: 'MessageHandler.idAware'}) SET p.name='idAware', p.kind='boolean', p.description='True if it implements IIdAwareMessageHandler<T> (inbox idempotency guard).';
MATCH (et:EntityType {name:'MessageHandler'}), (p:Property) WHERE p.id STARTS WITH 'MessageHandler.' MERGE (et)-[:HAS_PROPERTY]->(p);

MERGE (p:Ontology:Property {id: 'Job.taskName'})     SET p.name='taskName',     p.kind='string', p.description='IScheduledTask.TaskName.';
MERGE (p:Ontology:Property {id: 'Job.triggerModes'}) SET p.name='triggerModes', p.kind='string[]', p.values=['Scheduled','Manual','Deferred'], p.description='Statically-known modes only (Manual is always implicitly available and not tracked here); Scheduled cron expression itself is runtime DB data, not captured.';
MATCH (et:EntityType {name:'Job'}), (p:Property) WHERE p.id STARTS WITH 'Job.' MERGE (et)-[:HAS_PROPERTY]->(p);

MERGE (p:Ontology:Property {id: 'Role.name'})   SET p.name='name',   p.kind='string';
MERGE (p:Ontology:Property {id: 'Policy.name'}) SET p.name='name', p.kind='string';
MATCH (et:EntityType {name:'Role'}), (p:Property) WHERE p.id STARTS WITH 'Role.' MERGE (et)-[:HAS_PROPERTY]->(p);
MATCH (et:EntityType {name:'Policy'}), (p:Property) WHERE p.id STARTS WITH 'Policy.' MERGE (et)-[:HAS_PROPERTY]->(p);

// =============================================================================
// 4. RELATION TYPES
// =============================================================================
MERGE (rt:Ontology:RelationType {name: 'CONTAINS'})     SET rt.label = 'Contains';
MERGE (rt:Ontology:RelationType {name: 'PERSISTED_BY'}) SET rt.label = 'Persisted by';
MERGE (rt:Ontology:RelationType {name: 'OPERATES_ON'})  SET rt.label = 'Operates on';
MERGE (rt:Ontology:RelationType {name: 'EXPOSED_BY'})   SET rt.label = 'Exposed by';
MERGE (rt:Ontology:RelationType {name: 'PUBLISHES'})    SET rt.label = 'Publishes';
MERGE (rt:Ontology:RelationType {name: 'HANDLED_BY'})   SET rt.label = 'Handled by',
    rt.description = 'Deliberately shared verb for both Message resolution (0..N MessageHandlers, async) and Query resolution (exactly 1 QueryHandler, sync) — the two channels are told apart by node label, not by verb, per the design goal of "distinguish by type, unify by verb".';
MERGE (rt:Ontology:RelationType {name: 'USES'})         SET rt.label = 'Uses';
MERGE (rt:Ontology:RelationType {name: 'SCHEDULES'})    SET rt.label = 'Schedules',
    rt.description = 'An Action or a MessageHandler dynamically enqueues a Deferred Job via IDeferredJobScheduler.ScheduleAsync.';
MERGE (rt:Ontology:RelationType {name: 'GOVERNED_BY'})  SET rt.label = 'Governed by';
MERGE (rt:Ontology:RelationType {name: 'DEPENDS_ON'})   SET rt.label = 'Depends on';

// =============================================================================
// 5. RELATION RULES
// =============================================================================
MERGE (r:Ontology:RelationRule {id: 'System__CONTAINS__Host'});
MATCH (r:RelationRule {id:'System__CONTAINS__Host'}), (s:EntityType{name:'System'}), (t:EntityType{name:'Host'}), (rt:RelationType{name:'CONTAINS'})
MERGE (r)-[:FROM]->(s) MERGE (r)-[:HAS_TYPE]->(rt) MERGE (r)-[:TO]->(t);

MERGE (r:Ontology:RelationRule {id: 'System__CONTAINS__Module'});
MATCH (r:RelationRule {id:'System__CONTAINS__Module'}), (s:EntityType{name:'System'}), (t:EntityType{name:'Module'}), (rt:RelationType{name:'CONTAINS'})
MERGE (r)-[:FROM]->(s) MERGE (r)-[:HAS_TYPE]->(rt) MERGE (r)-[:TO]->(t);

MERGE (r:Ontology:RelationRule {id: 'Host__CONTAINS__Endpoint'});
MATCH (r:RelationRule {id:'Host__CONTAINS__Endpoint'}), (s:EntityType{name:'Host'}), (t:EntityType{name:'Endpoint'}), (rt:RelationType{name:'CONTAINS'})
MERGE (r)-[:FROM]->(s) MERGE (r)-[:HAS_TYPE]->(rt) MERGE (r)-[:TO]->(t);

MERGE (r:Ontology:RelationRule {id: 'Host__CONTAINS__Page'});
MATCH (r:RelationRule {id:'Host__CONTAINS__Page'}), (s:EntityType{name:'Host'}), (t:EntityType{name:'Page'}), (rt:RelationType{name:'CONTAINS'})
MERGE (r)-[:FROM]->(s) MERGE (r)-[:HAS_TYPE]->(rt) MERGE (r)-[:TO]->(t);

MERGE (r:Ontology:RelationRule {id: 'Host__CONTAINS__ScriptModule'});
MATCH (r:RelationRule {id:'Host__CONTAINS__ScriptModule'}), (s:EntityType{name:'Host'}), (t:EntityType{name:'ScriptModule'}), (rt:RelationType{name:'CONTAINS'})
MERGE (r)-[:FROM]->(s) MERGE (r)-[:HAS_TYPE]->(rt) MERGE (r)-[:TO]->(t);

MERGE (r:Ontology:RelationRule {id: 'Module__CONTAINS__Entity'});
MATCH (r:RelationRule {id:'Module__CONTAINS__Entity'}), (s:EntityType{name:'Module'}), (t:EntityType{name:'Entity'}), (rt:RelationType{name:'CONTAINS'})
MERGE (r)-[:FROM]->(s) MERGE (r)-[:HAS_TYPE]->(rt) MERGE (r)-[:TO]->(t);

MERGE (r:Ontology:RelationRule {id: 'Module__CONTAINS__Repository'});
MATCH (r:RelationRule {id:'Module__CONTAINS__Repository'}), (s:EntityType{name:'Module'}), (t:EntityType{name:'Repository'}), (rt:RelationType{name:'CONTAINS'})
MERGE (r)-[:FROM]->(s) MERGE (r)-[:HAS_TYPE]->(rt) MERGE (r)-[:TO]->(t);

MERGE (r:Ontology:RelationRule {id: 'Module__CONTAINS__Action'});
MATCH (r:RelationRule {id:'Module__CONTAINS__Action'}), (s:EntityType{name:'Module'}), (t:EntityType{name:'Action'}), (rt:RelationType{name:'CONTAINS'})
MERGE (r)-[:FROM]->(s) MERGE (r)-[:HAS_TYPE]->(rt) MERGE (r)-[:TO]->(t);

MERGE (r:Ontology:RelationRule {id: 'Module__CONTAINS__Job'});
MATCH (r:RelationRule {id:'Module__CONTAINS__Job'}), (s:EntityType{name:'Module'}), (t:EntityType{name:'Job'}), (rt:RelationType{name:'CONTAINS'})
MERGE (r)-[:FROM]->(s) MERGE (r)-[:HAS_TYPE]->(rt) MERGE (r)-[:TO]->(t);

MERGE (r:Ontology:RelationRule {id: 'Module__CONTAINS__MessageHandler'});
MATCH (r:RelationRule {id:'Module__CONTAINS__MessageHandler'}), (s:EntityType{name:'Module'}), (t:EntityType{name:'MessageHandler'}), (rt:RelationType{name:'CONTAINS'})
MERGE (r)-[:FROM]->(s) MERGE (r)-[:HAS_TYPE]->(rt) MERGE (r)-[:TO]->(t);

MERGE (r:Ontology:RelationRule {id: 'Module__CONTAINS__QueryHandler'});
MATCH (r:RelationRule {id:'Module__CONTAINS__QueryHandler'}), (s:EntityType{name:'Module'}), (t:EntityType{name:'QueryHandler'}), (rt:RelationType{name:'CONTAINS'})
MERGE (r)-[:FROM]->(s) MERGE (r)-[:HAS_TYPE]->(rt) MERGE (r)-[:TO]->(t);

MERGE (r:Ontology:RelationRule {id: 'Entity__PERSISTED_BY__Repository'});
MATCH (r:RelationRule {id:'Entity__PERSISTED_BY__Repository'}), (s:EntityType{name:'Entity'}), (t:EntityType{name:'Repository'}), (rt:RelationType{name:'PERSISTED_BY'})
MERGE (r)-[:FROM]->(s) MERGE (r)-[:HAS_TYPE]->(rt) MERGE (r)-[:TO]->(t);

MERGE (r:Ontology:RelationRule {id: 'Action__OPERATES_ON__Entity'});
MATCH (r:RelationRule {id:'Action__OPERATES_ON__Entity'}), (s:EntityType{name:'Action'}), (t:EntityType{name:'Entity'}), (rt:RelationType{name:'OPERATES_ON'})
MERGE (r)-[:FROM]->(s) MERGE (r)-[:HAS_TYPE]->(rt) MERGE (r)-[:TO]->(t);

MERGE (r:Ontology:RelationRule {id: 'Job__OPERATES_ON__Entity'});
MATCH (r:RelationRule {id:'Job__OPERATES_ON__Entity'}), (s:EntityType{name:'Job'}), (t:EntityType{name:'Entity'}), (rt:RelationType{name:'OPERATES_ON'})
MERGE (r)-[:FROM]->(s) MERGE (r)-[:HAS_TYPE]->(rt) MERGE (r)-[:TO]->(t);

MERGE (r:Ontology:RelationRule {id: 'Action__EXPOSED_BY__Endpoint'});
MATCH (r:RelationRule {id:'Action__EXPOSED_BY__Endpoint'}), (s:EntityType{name:'Action'}), (t:EntityType{name:'Endpoint'}), (rt:RelationType{name:'EXPOSED_BY'})
MERGE (r)-[:FROM]->(s) MERGE (r)-[:HAS_TYPE]->(rt) MERGE (r)-[:TO]->(t);

MERGE (r:Ontology:RelationRule {id: 'Action__EXPOSED_BY__Page'});
MATCH (r:RelationRule {id:'Action__EXPOSED_BY__Page'}), (s:EntityType{name:'Action'}), (t:EntityType{name:'Page'}), (rt:RelationType{name:'EXPOSED_BY'})
MERGE (r)-[:FROM]->(s) MERGE (r)-[:HAS_TYPE]->(rt) MERGE (r)-[:TO]->(t);

MERGE (r:Ontology:RelationRule {id: 'Action__PUBLISHES__Message'});
MATCH (r:RelationRule {id:'Action__PUBLISHES__Message'}), (s:EntityType{name:'Action'}), (t:EntityType{name:'Message'}), (rt:RelationType{name:'PUBLISHES'})
MERGE (r)-[:FROM]->(s) MERGE (r)-[:HAS_TYPE]->(rt) MERGE (r)-[:TO]->(t);

MERGE (r:Ontology:RelationRule {id: 'Job__PUBLISHES__Message'});
MATCH (r:RelationRule {id:'Job__PUBLISHES__Message'}), (s:EntityType{name:'Job'}), (t:EntityType{name:'Message'}), (rt:RelationType{name:'PUBLISHES'})
MERGE (r)-[:FROM]->(s) MERGE (r)-[:HAS_TYPE]->(rt) MERGE (r)-[:TO]->(t);

MERGE (r:Ontology:RelationRule {id: 'Message__HANDLED_BY__MessageHandler'});
MATCH (r:RelationRule {id:'Message__HANDLED_BY__MessageHandler'}), (s:EntityType{name:'Message'}), (t:EntityType{name:'MessageHandler'}), (rt:RelationType{name:'HANDLED_BY'})
MERGE (r)-[:FROM]->(s) MERGE (r)-[:HAS_TYPE]->(rt) MERGE (r)-[:TO]->(t);

MERGE (r:Ontology:RelationRule {id: 'Action__USES__Query'});
MATCH (r:RelationRule {id:'Action__USES__Query'}), (s:EntityType{name:'Action'}), (t:EntityType{name:'Query'}), (rt:RelationType{name:'USES'})
MERGE (r)-[:FROM]->(s) MERGE (r)-[:HAS_TYPE]->(rt) MERGE (r)-[:TO]->(t);

MERGE (r:Ontology:RelationRule {id: 'Query__HANDLED_BY__QueryHandler'});
MATCH (r:RelationRule {id:'Query__HANDLED_BY__QueryHandler'}), (s:EntityType{name:'Query'}), (t:EntityType{name:'QueryHandler'}), (rt:RelationType{name:'HANDLED_BY'})
MERGE (r)-[:FROM]->(s) MERGE (r)-[:HAS_TYPE]->(rt) MERGE (r)-[:TO]->(t);

MERGE (r:Ontology:RelationRule {id: 'Action__SCHEDULES__Job'});
MATCH (r:RelationRule {id:'Action__SCHEDULES__Job'}), (s:EntityType{name:'Action'}), (t:EntityType{name:'Job'}), (rt:RelationType{name:'SCHEDULES'})
MERGE (r)-[:FROM]->(s) MERGE (r)-[:HAS_TYPE]->(rt) MERGE (r)-[:TO]->(t);

MERGE (r:Ontology:RelationRule {id: 'MessageHandler__SCHEDULES__Job'});
MATCH (r:RelationRule {id:'MessageHandler__SCHEDULES__Job'}), (s:EntityType{name:'MessageHandler'}), (t:EntityType{name:'Job'}), (rt:RelationType{name:'SCHEDULES'})
MERGE (r)-[:FROM]->(s) MERGE (r)-[:HAS_TYPE]->(rt) MERGE (r)-[:TO]->(t);

MERGE (r:Ontology:RelationRule {id: 'Endpoint__GOVERNED_BY__Role'});
MATCH (r:RelationRule {id:'Endpoint__GOVERNED_BY__Role'}), (s:EntityType{name:'Endpoint'}), (t:EntityType{name:'Role'}), (rt:RelationType{name:'GOVERNED_BY'})
MERGE (r)-[:FROM]->(s) MERGE (r)-[:HAS_TYPE]->(rt) MERGE (r)-[:TO]->(t);

MERGE (r:Ontology:RelationRule {id: 'Endpoint__GOVERNED_BY__Policy'});
MATCH (r:RelationRule {id:'Endpoint__GOVERNED_BY__Policy'}), (s:EntityType{name:'Endpoint'}), (t:EntityType{name:'Policy'}), (rt:RelationType{name:'GOVERNED_BY'})
MERGE (r)-[:FROM]->(s) MERGE (r)-[:HAS_TYPE]->(rt) MERGE (r)-[:TO]->(t);

MERGE (r:Ontology:RelationRule {id: 'Page__GOVERNED_BY__Role'});
MATCH (r:RelationRule {id:'Page__GOVERNED_BY__Role'}), (s:EntityType{name:'Page'}), (t:EntityType{name:'Role'}), (rt:RelationType{name:'GOVERNED_BY'})
MERGE (r)-[:FROM]->(s) MERGE (r)-[:HAS_TYPE]->(rt) MERGE (r)-[:TO]->(t);

MERGE (r:Ontology:RelationRule {id: 'Page__GOVERNED_BY__Policy'});
MATCH (r:RelationRule {id:'Page__GOVERNED_BY__Policy'}), (s:EntityType{name:'Page'}), (t:EntityType{name:'Policy'}), (rt:RelationType{name:'GOVERNED_BY'})
MERGE (r)-[:FROM]->(s) MERGE (r)-[:HAS_TYPE]->(rt) MERGE (r)-[:TO]->(t);

MERGE (r:Ontology:RelationRule {id: 'Page__USES__Endpoint'})
SET r.description = 'Client-side JS embedded in a Razor view calls an API endpoint directly (e.g. fetch(...) in an AMD module) — bypasses server-side C# entirely, only discoverable by also parsing the JS layer.';
MATCH (r:RelationRule {id:'Page__USES__Endpoint'}), (s:EntityType{name:'Page'}), (t:EntityType{name:'Endpoint'}), (rt:RelationType{name:'USES'})
MERGE (r)-[:FROM]->(s) MERGE (r)-[:HAS_TYPE]->(rt) MERGE (r)-[:TO]->(t);

MERGE (r:Ontology:RelationRule {id: 'Page__USES__ScriptModule'});
MATCH (r:RelationRule {id:'Page__USES__ScriptModule'}), (s:EntityType{name:'Page'}), (t:EntityType{name:'ScriptModule'}), (rt:RelationType{name:'USES'})
MERGE (r)-[:FROM]->(s) MERGE (r)-[:HAS_TYPE]->(rt) MERGE (r)-[:TO]->(t);

MERGE (r:Ontology:RelationRule {id: 'ScriptModule__DEPENDS_ON__ScriptModule'})
SET r.description = 'define([...dependency names...], factory) in a ScriptModule file.';
MATCH (r:RelationRule {id:'ScriptModule__DEPENDS_ON__ScriptModule'}), (s:EntityType{name:'ScriptModule'}), (t:EntityType{name:'ScriptModule'}), (rt:RelationType{name:'DEPENDS_ON'})
MERGE (r)-[:FROM]->(s) MERGE (r)-[:HAS_TYPE]->(rt) MERGE (r)-[:TO]->(t);

// =============================================================================
// NOTE — deliberately NOT a stored relation rule: Module-to-module coupling.
// It is a derived/computed traversal (Module-CONTAINS->Action-PUBLISHES->
// Message-HANDLED_BY->MessageHandler<-CONTAINS-Module, and the Query
// equivalent), exposed as the GetModuleDependencies Tier-1 MCP tool —
// NOT a materialized DEPENDS_ON edge between Modules, to avoid a second
// source of truth that can silently drift from the real PUBLISHES/HANDLED_BY
// edges it would be derived from.
// =============================================================================

// =============================================================================
// Confirmed bounded-context instances (Layer 1 — for the parser's module list,
// NOT part of the ontology schema itself; recorded here for traceability back
// to this design conversation):
//   AccountProfile, Backoffice (no own entities — admin facade by design),
//   Catalog (merges Products+Images), IAM, Inventory (folder: Availability),
//   Checkout (folder: Presale/Checkout), Orders, Payments, Coupons,
//   Fulfillment, Communication, Currencies, TimeManagement, Messaging
// =============================================================================
