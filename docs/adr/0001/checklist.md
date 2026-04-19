## Conformance checklist

- [ ] Solution contains all 7 projects
- [ ] `ExceptionMiddleware` is registered in both `Web/Startup.cs` and `API/Startup.cs`
- [ ] `AbstractService` exists in `Application/Abstracts/` and is used by reference/lookup services
- [ ] No raw `System.IO` calls outside `IFileStore`/`IFileWrapper` abstractions
