# Architecture Guide

## Core Concepts

### Service Layer

#### Request Handling

##### Synchronous Path
The synchronous request path is the primary processing route for all incoming API
calls. Requests are validated against the schema, authenticated via the identity
provider, and dispatched to the appropriate domain handler. Each handler returns
a typed result that the API layer maps to an HTTP response code and body.

##### Asynchronous Path
The asynchronous request path queues work items into the internal job broker and
returns an accepted status immediately to the caller. Background workers pick up
the queued items, execute the domain logic, and publish completion events. Callers
poll a status endpoint or subscribe to webhook notifications to learn the outcome.

#### Response Formatting

The response formatter applies content negotiation, serialises the domain result
into the requested media type, attaches correlation headers, and finalises cache
control directives before returning to the transport layer.

## Infrastructure

Infrastructure services provide persistence, messaging, and external integrations
used by the service layer described above.
