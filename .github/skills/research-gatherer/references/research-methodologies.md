# Research Methodologies Reference

This reference is gather-only. It helps the research-gatherer choose how to collect evidence.

## Research types

### Internal

Use when the answer lives inside ECommerceApp.

Typical sources:

- source code
- tests
- configuration
- ADRs
- `.github/context/*.md`
- project docs

### External

Use when the answer lives outside the repository.

Typical sources:

- official docs
- standards
- framework docs
- vendor docs
- public engineering blogs

### Mixed

Use when both internal and external evidence are needed.

Typical examples:

- compare ECommerceApp behavior to a standard pattern
- check whether the repo implementation matches public guidance
- validate a design decision against current external practice

## Source categories

### Codebase

- implementation files
- tests
- handlers, services, controllers, adapters

### Documentation

- ADRs
- architecture docs
- README files
- workflow specs

### Configuration

- `.json`, `.yml`, `.yaml`
- `.env` examples
- docker and CI config

### External

- official product docs
- public APIs
- standards
- public case studies

## Collection strategy

1. Broad discovery
2. Targeted reading
3. Deep dive
4. Verification

## Confidence rules

- High: multiple sources agree
- Medium: one strong source plus partial corroboration
- Low: thin or contradictory evidence

## Pitfalls

- scope creep
- source hallucination
- skipping verification
- mixing evidence with conclusions too early
