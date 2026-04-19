## Conformance checklist

- [ ] Every aggregate ID
- [ ] Every value object is a `sealed record` with validation in the constructor
- [ ] `DomainException` used for VO validation failures (not `ArgumentException`)
- [ ] Shared VOs (`Price`, `Money`, `Slug`, `DomainException`) live in `Domain/Shared/`
- [ ] BC-specific VOs live under `Domain/<Group>/<BcName>/ValueObjects/`
- [ ] EF Core `HasConversion` configured for every typed ID and VO in entity configurations
