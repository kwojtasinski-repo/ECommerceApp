## Conformance checklist

- [x] All properties on `Domain/Supporting/Currencies/Currency.cs` use `private set`
- [x] All properties on `Domain/Supporting/Currencies/CurrencyRate.cs` use `private set`
- [x] `Currency` has a `private` parameterless constructor for EF Core materialization
- [x] `CurrencyRate` has a `private` parameterless constructor for EF Core materialization
- [x] `Currency.Create(string, string)` is the only public construction path for `Currency`
- [x] `CurrencyRate.Create(CurrencyId, decimal, DateTime)` is the only public construction path for `CurrencyRate`
- [x] `CurrencyRate.Create` throws `DomainException` when `rate <= 0`
- [x] `CurrencyRate.Create` throws `DomainException` when `currencyId` is `null`
- [x] `CurrencyCode` is a `sealed record`; constructor enforces exactly 3 characters and normalises to uppercase
- [x] `CurrencyDescription` is a `sealed record`; constructor enforces non-empty and max 300 characters
- [x] `CurrencyId` and `CurrencyRateId` are `sealed record` types inheriting `TypedId<int>`
- [x] `Domain/Supporting/Currencies/Currency.cs` has no navigation properties to `Payment`, `Order`, or `Item`
- [x] `Domain/Supporting/Currencies/CurrencyRate.cs` has no `Currency` navigation property — `CurrencyId` typed ID only

---
