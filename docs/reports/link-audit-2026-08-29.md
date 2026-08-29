# Audyt linkow w docs/ i assets/ - 2026-08-29

Brief dla naprawy (do przekazania agentowi wykonujacemu fix). Wygenerowane automatycznym skryptem, ktory
wyciagnal wszystkie linki markdown z `docs/**/*.md`, `assets/**/*.md` i `README.md`, rozwiazal je wzgledem
polozenia pliku zrodlowego i sprawdzil istnienie targetu na dysku.

- Linki przeskanowane: **740**
- Linki zepsute: **221**

## Zasady dla poprawek (przeczytaj przed edycja)

1. **Nigdy nie uzywaj `../..` gdy wystarczy mniej poziomow, i nigdy nie zgaduj liczby `../` - policz je z
   rzeczywistej sciezki pliku do celu.** Kolumna "Sugerowana poprawka" ponizej jest juz policzona i
   zweryfikowana (target istnieje na dysku) - mozna ja wkleic 1:1.
2. Linki wzgledne dzialaja identycznie lokalnie i na GitHub.com, o ile nie maja wiodacego `/` (ktory na
   GitHubie liczy sie od korzenia repo, a lokalnie czesto od korzenia filesystemu - unikac).
3. Po kazdej poprawce w danym pliku odpalic ponownie walidator (opisany na koncu tego pliku), zeby nie
   zostawic literowki.
4. Kategoria C (retired context mode) **nie jest** zwyklym fixem sciezki - target realnie nie istnieje
   w repo (funkcja usunieta w `2b16db64`). Do decyzji: usunac caly link/zdanie, albo podmienic na
   aktualny odpowiednik dokumentu, jesli taki istnieje.
5. Kategoria F to odniesienia do efemerycznych artefaktow w **datowanych raportach historycznych**
   (maj 2026). Nie trzeba ich "naprawiac" sciezka - to zrzuty punktu w czasie. Rekomendacja: dopisac
   adnotacje w stylu `(plik pomocniczy, nieobecny w repo)` albo zostawic jako dokumentacje historyczna.

## Podsumowanie kategorii

| Kategoria | Liczba linkow |
|---|---|
| A. Restrukturyzacja ADR-ow (plaski plik -> folder na numer) | 151 |
| B. Przemianowane pliki .github/instructions/* | 18 |
| E. Zbedny prefiks "docs/" wewnatrz plikow juz w docs/ | 7 |
| D. Zle/niepelne sciezki wzgledne (nie zwiazane z restrukturyzacja ADR) | 33 |
| C. Odniesienia do skasowanego "context mode" (target nie istnieje - decyzja tresciowa, nie tylko sciezka) | 8 |
| F. Odniesienia do efemerycznych/skasowanych artefaktow (raporty historyczne) | 4 |

---

## A. Restrukturyzacja ADR-ow (plaski plik -> folder na numer) (151)

### `docs/adr/0002/0002-post-event-storming-architectural-evolution-strategy.md`

| Zepsuty link (target) | Sugerowana poprawka |
|---|---|
| `./0001-project-overview-and-technology-stack.md` | `../0001/0001-project-overview-and-technology-stack.md` |

### `docs/adr/0003/0003-feature-folder-organization-for-new-bounded-context-code.md`

| Zepsuty link (target) | Sugerowana poprawka |
|---|---|
| `./0001-project-overview-and-technology-stack.md` | `../0001/0001-project-overview-and-technology-stack.md` |
| `./0002-post-event-storming-architectural-evolution-strategy.md` | `../0002/0002-post-event-storming-architectural-evolution-strategy.md` |

### `docs/adr/0004/0004-module-taxonomy-and-bounded-context-grouping.md`

| Zepsuty link (target) | Sugerowana poprawka |
|---|---|
| `./0001-project-overview-and-technology-stack.md` | `../0001/0001-project-overview-and-technology-stack.md` |
| `./0002-post-event-storming-architectural-evolution-strategy.md` | `../0002/0002-post-event-storming-architectural-evolution-strategy.md` |
| `./0003-feature-folder-organization-for-new-bounded-context-code.md` | `../0003/0003-feature-folder-organization-for-new-bounded-context-code.md` |

### `docs/adr/0006/0006-typedid-and-value-objects-as-shared-domain-primitives.md`

| Zepsuty link (target) | Sugerowana poprawka |
|---|---|
| `./0001-project-overview-and-technology-stack.md` | `../0001/0001-project-overview-and-technology-stack.md` |
| `./0002-post-event-storming-architectural-evolution-strategy.md` | `../0002/0002-post-event-storming-architectural-evolution-strategy.md` |
| `./0005-accountprofile-bc-userprofile-aggregate-design.md` | `../0005/0005-accountprofile-bc-userprofile-aggregate-design.md` |
| `./0007-catalog-bc-product-category-tag-aggregate-design.md` | `../0007/0007-catalog-bc-product-category-tag-aggregate-design.md` |
| `./0011-inventory-availability-bc-design.md` | `../0011/0011-inventory-availability-bc-design.md` |

### `docs/adr/0008/0008-supporting-currencies-bc-design.md`

| Zepsuty link (target) | Sugerowana poprawka |
|---|---|
| `0002-post-event-storming-architectural-evolution-strategy.md` | `../0002/0002-post-event-storming-architectural-evolution-strategy.md` |
| `0003-feature-folder-organization-for-new-bounded-context-code.md` | `../0003/0003-feature-folder-organization-for-new-bounded-context-code.md` |
| `0004-module-taxonomy-and-bounded-context-grouping.md` | `../0004/0004-module-taxonomy-and-bounded-context-grouping.md` |
| `0006-typedid-and-value-objects-as-shared-domain-primitives.md` | `../0006/0006-typedid-and-value-objects-as-shared-domain-primitives.md` |
| `0007-catalog-bc-product-category-tag-aggregate-design.md` | `../0007/0007-catalog-bc-product-category-tag-aggregate-design.md` |

### `docs/adr/0010/0010-in-memory-message-broker-for-cross-bc-communication.md`

| Zepsuty link (target) | Sugerowana poprawka |
|---|---|
| `./0002-post-event-storming-architectural-evolution-strategy.md` | `../0002/0002-post-event-storming-architectural-evolution-strategy.md` |
| `./0004-module-taxonomy-and-bounded-context-grouping.md` | `../0004/0004-module-taxonomy-and-bounded-context-grouping.md` |
| `./0009-supporting-timemanagement-bc-design.md` | `../0009/0009-supporting-timemanagement-bc-design.md` |

### `docs/adr/0013/0013-per-bc-dbcontext-interfaces.md`

| Zepsuty link (target) | Sugerowana poprawka |
|---|---|
| `0002-post-event-storming-architectural-evolution-strategy.md` | `../0002/0002-post-event-storming-architectural-evolution-strategy.md` |
| `0004-module-taxonomy-and-bounded-context-grouping.md` | `../0004/0004-module-taxonomy-and-bounded-context-grouping.md` |
| `0007-catalog-bc-product-category-tag-aggregate-design.md` | `../0007/0007-catalog-bc-product-category-tag-aggregate-design.md` |
| `0008-supporting-currencies-bc-design.md` | `../0008/0008-supporting-currencies-bc-design.md` |
| `0009-supporting-timemanagement-bc-design.md` | `../0009/0009-supporting-timemanagement-bc-design.md` |
| `0011-inventory-availability-bc-design.md` | `../0011/0011-inventory-availability-bc-design.md` |
| `0012-presale-checkout-bc-design.md` | `../0012/0012-presale-checkout-bc-design.md` |

### `docs/adr/0018/0018-supporting-communication-bc-design.md`

| Zepsuty link (target) | Sugerowana poprawka |
|---|---|
| `./0002-post-event-storming-architectural-evolution-strategy.md` | `../0002/0002-post-event-storming-architectural-evolution-strategy.md` |
| `./0010-in-memory-message-broker-for-cross-bc-communication.md` | `../0010/0010-in-memory-message-broker-for-cross-bc-communication.md` |
| `./0014-sales-orders-bc-design.md` | `../0014/0014-sales-orders-bc-design.md` |
| `./0015-sales-payments-bc-design.md` | `../0015/0015-sales-payments-bc-design.md` |
| `./0016-sales-coupons-bc-design.md` | `../0016/0016-sales-coupons-bc-design.md` |
| `./0017-sales-fulfillment-bc-design.md` | `../0017/0017-sales-fulfillment-bc-design.md` |

### `docs/adr/0019/0019-identity-iam-bc-design.md`

| Zepsuty link (target) | Sugerowana poprawka |
|---|---|
| `./0002-post-event-storming-architectural-evolution-strategy.md` | `../0002/0002-post-event-storming-architectural-evolution-strategy.md` |
| `./0013-per-bc-dbcontext-interfaces.md` | `../0013/0013-per-bc-dbcontext-interfaces.md` |
| `./0014-sales-orders-bc-design.md` | `../0014/0014-sales-orders-bc-design.md` |

### `docs/adr/0020/0020-backoffice-bc-design.md`

| Zepsuty link (target) | Sugerowana poprawka |
|---|---|
| `./0002-post-event-storming-architectural-evolution-strategy.md` | `../0002/0002-post-event-storming-architectural-evolution-strategy.md` |
| `./0013-per-bc-dbcontext-interfaces.md` | `../0013/0013-per-bc-dbcontext-interfaces.md` |
| `./0003-feature-folder-organization-for-new-bounded-context-code.md` | `../0003/0003-feature-folder-organization-for-new-bounded-context-code.md` |
| `./0004-module-taxonomy-and-bounded-context-grouping.md` | `../0004/0004-module-taxonomy-and-bounded-context-grouping.md` |
| `./0005-accountprofile-bc-userprofile-aggregate-design.md` | `../0005/0005-accountprofile-bc-userprofile-aggregate-design.md` |
| `./0007-catalog-bc-product-category-tag-aggregate-design.md` | `../0007/0007-catalog-bc-product-category-tag-aggregate-design.md` |
| `./0014-sales-orders-bc-design.md` | `../0014/0014-sales-orders-bc-design.md` |
| `./0015-sales-payments-bc-design.md` | `../0015/0015-sales-payments-bc-design.md` |
| `./0016-sales-coupons-bc-design.md` | `../0016/0016-sales-coupons-bc-design.md` |
| `./0017-sales-fulfillment-bc-design.md` | `../0017/0017-sales-fulfillment-bc-design.md` |
| `./0019-identity-iam-bc-design.md` | `../0019/0019-identity-iam-bc-design.md` |

### `docs/adr/0021/0021-frontend-error-pipeline-and-js-migration-strategy.md`

| Zepsuty link (target) | Sugerowana poprawka |
|---|---|
| `./0001-project-overview-and-technology-stack.md` | `../0001/0001-project-overview-and-technology-stack.md` |
| `./0022-navbar-two-tier-redesign.md` | `../0022/0022-navbar-two-tier-redesign.md` |

### `docs/adr/0022/0022-navbar-two-tier-redesign.md`

| Zepsuty link (target) | Sugerowana poprawka |
|---|---|
| `./0001-project-overview-and-technology-stack.md` | `../0001/0001-project-overview-and-technology-stack.md` |
| `./0021-frontend-error-pipeline-and-js-migration-strategy.md` | `../0021/0021-frontend-error-pipeline-and-js-migration-strategy.md` |
| `./0011-inventory-availability-bc-design.md` | `../0011/0011-inventory-availability-bc-design.md` |

### `docs/adr/0024/0024-controller-routing-strategy.md`

| Zepsuty link (target) | Sugerowana poprawka |
|---|---|
| `0014-sales-orders-bc-design.md` | `../0014/0014-sales-orders-bc-design.md` |
| `0015-sales-payments-bc-design.md` | `../0015/0015-sales-payments-bc-design.md` |

### `docs/architecture/bounded-context-map.md`

| Zepsuty link (target) | Sugerowana poprawka |
|---|---|
| `../adr/0002-post-event-storming-architectural-evolution-strategy.md` | `../adr/0002/0002-post-event-storming-architectural-evolution-strategy.md` |
| `../adr/0004-module-taxonomy-and-bounded-context-grouping.md` | `../adr/0004/0004-module-taxonomy-and-bounded-context-grouping.md` |
| `../adr/0003-feature-folder-organization-for-new-bounded-context-code.md` | `../adr/0003/0003-feature-folder-organization-for-new-bounded-context-code.md` |
| `../adr/0007-catalog-bc-product-category-tag-aggregate-design.md` | `../adr/0007/0007-catalog-bc-product-category-tag-aggregate-design.md` |
| `../adr/0008-supporting-currencies-bc-design.md` | `../adr/0008/0008-supporting-currencies-bc-design.md` |
| `../adr/0009-supporting-timemanagement-bc-design.md` | `../adr/0009/0009-supporting-timemanagement-bc-design.md` |
| `../adr/0011-inventory-availability-bc-design.md` | `../adr/0011/0011-inventory-availability-bc-design.md` |
| `../adr/0012-presale-checkout-bc-design.md` | `../adr/0012/0012-presale-checkout-bc-design.md` |
| `../adr/0015-sales-payments-bc-design.md` | `../adr/0015/0015-sales-payments-bc-design.md` |
| `../adr/0013-per-bc-dbcontext-interfaces.md` | `../adr/0013/0013-per-bc-dbcontext-interfaces.md` |
| `../adr/0014-sales-orders-bc-design.md` | `../adr/0014/0014-sales-orders-bc-design.md` |
| `../adr/0016-sales-coupons-bc-design.md` | `../adr/0016/0016-sales-coupons-bc-design.md` |
| `../adr/0017-sales-fulfillment-bc-design.md` | `../adr/0017/0017-sales-fulfillment-bc-design.md` |
| `../adr/0018-supporting-communication-bc-design.md` | `../adr/0018/0018-supporting-communication-bc-design.md` |
| `../adr/0019-identity-iam-bc-design.md` | `../adr/0019/0019-identity-iam-bc-design.md` |
| `../adr/0020-backoffice-bc-design.md` | `../adr/0020/0020-backoffice-bc-design.md` |
| `../adr/0019-identity-iam-bc-design.md` | `../adr/0019/0019-identity-iam-bc-design.md` |
| `../adr/0005-accountprofile-bc-userprofile-aggregate-design.md` | `../adr/0005/0005-accountprofile-bc-userprofile-aggregate-design.md` |
| `../adr/0006-typedid-and-value-objects-as-shared-domain-primitives.md` | `../adr/0006/0006-typedid-and-value-objects-as-shared-domain-primitives.md` |
| `../adr/0007-catalog-bc-product-category-tag-aggregate-design.md` | `../adr/0007/0007-catalog-bc-product-category-tag-aggregate-design.md` |
| `../adr/0008-supporting-currencies-bc-design.md` | `../adr/0008/0008-supporting-currencies-bc-design.md` |
| `../adr/0006-typedid-and-value-objects-as-shared-domain-primitives.md` | `../adr/0006/0006-typedid-and-value-objects-as-shared-domain-primitives.md` |
| `../adr/0009-supporting-timemanagement-bc-design.md` | `../adr/0009/0009-supporting-timemanagement-bc-design.md` |
| `../adr/0011-inventory-availability-bc-design.md` | `../adr/0011/0011-inventory-availability-bc-design.md` |
| `../adr/0012-presale-checkout-bc-design.md` | `../adr/0012/0012-presale-checkout-bc-design.md` |
| `../adr/0010-in-memory-message-broker-for-cross-bc-communication.md` | `../adr/0010/0010-in-memory-message-broker-for-cross-bc-communication.md` |
| `../adr/0014-sales-orders-bc-design.md` | `../adr/0014/0014-sales-orders-bc-design.md` |
| `../adr/0015-sales-payments-bc-design.md` | `../adr/0015/0015-sales-payments-bc-design.md` |
| `../adr/0016-sales-coupons-bc-design.md` | `../adr/0016/0016-sales-coupons-bc-design.md` |
| `../adr/0017-sales-fulfillment-bc-design.md` | `../adr/0017/0017-sales-fulfillment-bc-design.md` |
| `../adr/0018-supporting-communication-bc-design.md` | `../adr/0018/0018-supporting-communication-bc-design.md` |
| `../adr/0020-backoffice-bc-design.md` | `../adr/0020/0020-backoffice-bc-design.md` |
| `../adr/0013-per-bc-dbcontext-interfaces.md` | `../adr/0013/0013-per-bc-dbcontext-interfaces.md` |
| `../adr/0012-presale-checkout-bc-design.md` | `../adr/0012/0012-presale-checkout-bc-design.md` |
| `../adr/0021-frontend-error-pipeline-and-js-migration-strategy.md` | `../adr/0021/0021-frontend-error-pipeline-and-js-migration-strategy.md` |
| `../adr/0018-supporting-communication-bc-design.md` | `../adr/0018/0018-supporting-communication-bc-design.md` |
| `../adr/0019-identity-iam-bc-design.md` | `../adr/0019/0019-identity-iam-bc-design.md` |
| `../adr/0020-backoffice-bc-design.md` | `../adr/0020/0020-backoffice-bc-design.md` |
| `../adr/0008-supporting-currencies-bc-design.md` | `../adr/0008/0008-supporting-currencies-bc-design.md` |
| `../adr/0001-project-overview-and-technology-stack.md` | `../adr/0001/0001-project-overview-and-technology-stack.md` |
| `../adr/0002-post-event-storming-architectural-evolution-strategy.md` | `../adr/0002/0002-post-event-storming-architectural-evolution-strategy.md` |
| `../adr/0005-accountprofile-bc-userprofile-aggregate-design.md` | `../adr/0005/0005-accountprofile-bc-userprofile-aggregate-design.md` |
| `../adr/0006-typedid-and-value-objects-as-shared-domain-primitives.md` | `../adr/0006/0006-typedid-and-value-objects-as-shared-domain-primitives.md` |
| `../adr/0007-catalog-bc-product-category-tag-aggregate-design.md` | `../adr/0007/0007-catalog-bc-product-category-tag-aggregate-design.md` |
| `../adr/0008-supporting-currencies-bc-design.md` | `../adr/0008/0008-supporting-currencies-bc-design.md` |
| `../adr/0009-supporting-timemanagement-bc-design.md` | `../adr/0009/0009-supporting-timemanagement-bc-design.md` |
| `../adr/0013-per-bc-dbcontext-interfaces.md` | `../adr/0013/0013-per-bc-dbcontext-interfaces.md` |
| `../adr/0014-sales-orders-bc-design.md` | `../adr/0014/0014-sales-orders-bc-design.md` |
| `../adr/0015-sales-payments-bc-design.md` | `../adr/0015/0015-sales-payments-bc-design.md` |
| `../adr/0016-sales-coupons-bc-design.md` | `../adr/0016/0016-sales-coupons-bc-design.md` |
| `../adr/0017-sales-fulfillment-bc-design.md` | `../adr/0017/0017-sales-fulfillment-bc-design.md` |
| `../adr/0018-supporting-communication-bc-design.md` | `../adr/0018/0018-supporting-communication-bc-design.md` |
| `../adr/0019-identity-iam-bc-design.md` | `../adr/0019/0019-identity-iam-bc-design.md` |
| `../adr/0020-backoffice-bc-design.md` | `../adr/0020/0020-backoffice-bc-design.md` |
| `../adr/0021-frontend-error-pipeline-and-js-migration-strategy.md` | `../adr/0021/0021-frontend-error-pipeline-and-js-migration-strategy.md` |

### `docs/playbooks/rag-bootstrap.md`

| Zepsuty link (target) | Sugerowana poprawka |
|---|---|
| `../adr/0027/0027-rag-pipeline.md` | `../adr/0027/0027-rag-pipeline-design.md` |

### `docs/reference/endpoint-map.md`

| Zepsuty link (target) | Sugerowana poprawka |
|---|---|
| `../adr/0024-controller-routing-strategy.md` | `../adr/0024/0024-controller-routing-strategy.md` |
| `../adr/0025-api-tiered-access-trusted-purchase-policy.md` | `../adr/0025/0025-api-tiered-access-trusted-purchase-policy.md` |
| `../adr/0024-controller-routing-strategy.md` | `../adr/0024/0024-controller-routing-strategy.md` |
| `../adr/0005-accountprofile-bc-userprofile-aggregate-design.md` | `../adr/0005/0005-accountprofile-bc-userprofile-aggregate-design.md` |
| `../adr/0007-catalog-bc-product-category-tag-aggregate-design.md` | `../adr/0007/0007-catalog-bc-product-category-tag-aggregate-design.md` |
| `../adr/0008-supporting-currencies-bc-design.md` | `../adr/0008/0008-supporting-currencies-bc-design.md` |
| `../adr/0016-sales-coupons-bc-design.md` | `../adr/0016/0016-sales-coupons-bc-design.md` |
| `../adr/0017-sales-fulfillment-bc-design.md` | `../adr/0017/0017-sales-fulfillment-bc-design.md` |

### `docs/reports/web-ui-views-report.md`

| Zepsuty link (target) | Sugerowana poprawka |
|---|---|
| `../adr/0007-catalog-bc-product-category-tag-aggregate-design.md` | `../adr/0007/0007-catalog-bc-product-category-tag-aggregate-design.md` |
| `../adr/0005-accountprofile-bc-userprofile-aggregate-design.md` | `../adr/0005/0005-accountprofile-bc-userprofile-aggregate-design.md` |
| `../adr/0017-sales-fulfillment-bc-design.md` | `../adr/0017/0017-sales-fulfillment-bc-design.md` |

### `docs/roadmap/README.md`

| Zepsuty link (target) | Sugerowana poprawka |
|---|---|
| `../adr/0014-sales-orders-bc-design.md` | `../adr/0014/0014-sales-orders-bc-design.md` |
| `../adr/0015-sales-payments-bc-design.md` | `../adr/0015/0015-sales-payments-bc-design.md` |
| `../adr/0016-sales-coupons-bc-design.md` | `../adr/0016/0016-sales-coupons-bc-design.md` |
| `../adr/0012-presale-checkout-bc-design.md` | `../adr/0012/0012-presale-checkout-bc-design.md` |
| `../adr/0019-identity-iam-bc-design.md` | `../adr/0019/0019-identity-iam-bc-design.md` |
| `../adr/0019-identity-iam-bc-design.md` | `../adr/0019/0019-identity-iam-bc-design.md` |
| `../adr/0021-frontend-error-pipeline-and-js-migration-strategy.md` | `../adr/0021/0021-frontend-error-pipeline-and-js-migration-strategy.md` |
| `../adr/0024-controller-routing-strategy.md` | `../adr/0024/0024-controller-routing-strategy.md` |
| `../adr/0017-sales-fulfillment-bc-design.md` | `../adr/0017/0017-sales-fulfillment-bc-design.md` |
| `../adr/0011-inventory-availability-bc-design.md` | `../adr/0011/0011-inventory-availability-bc-design.md` |
| `../adr/0014-sales-orders-bc-design.md` | `../adr/0014/0014-sales-orders-bc-design.md` |
| `../adr/0014-sales-orders-bc-design.md` | `../adr/0014/0014-sales-orders-bc-design.md` |
| `../adr/0018-supporting-communication-bc-design.md` | `../adr/0018/0018-supporting-communication-bc-design.md` |
| `../adr/0016-sales-coupons-bc-design.md` | `../adr/0016/0016-sales-coupons-bc-design.md` |
| `../adr/0016-sales-coupons-bc-design.md` | `../adr/0016/0016-sales-coupons-bc-design.md` |

### `docs/roadmap/frontend-pipeline.md`

| Zepsuty link (target) | Sugerowana poprawka |
|---|---|
| `../adr/0021-frontend-error-pipeline-and-js-migration-strategy.md` | `../adr/0021/0021-frontend-error-pipeline-and-js-migration-strategy.md` |
| `../adr/0021-frontend-error-pipeline-and-js-migration-strategy.md` | `../adr/0021/0021-frontend-error-pipeline-and-js-migration-strategy.md` |

### `docs/roadmap/iam-atomic-switch.md`

| Zepsuty link (target) | Sugerowana poprawka |
|---|---|
| `../adr/0019-identity-iam-bc-design.md` | `../adr/0019/0019-identity-iam-bc-design.md` |
| `../adr/0019-identity-iam-bc-design.md` | `../adr/0019/0019-identity-iam-bc-design.md` |

### `docs/roadmap/iam-refresh-token.md`

| Zepsuty link (target) | Sugerowana poprawka |
|---|---|
| `../adr/0019-identity-iam-bc-design.md` | `../adr/0019/0019-identity-iam-bc-design.md` |

### `docs/roadmap/orders-atomic-switch.md`

| Zepsuty link (target) | Sugerowana poprawka |
|---|---|
| `../adr/0014-sales-orders-bc-design.md` | `../adr/0014/0014-sales-orders-bc-design.md` |
| `../adr/0025-api-tiered-access-trusted-purchase-policy.md` | `../adr/0025/0025-api-tiered-access-trusted-purchase-policy.md` |
| `../adr/0014-sales-orders-bc-design.md` | `../adr/0014/0014-sales-orders-bc-design.md` |
| `../adr/0024-controller-routing-strategy.md` | `../adr/0024/0024-controller-routing-strategy.md` |
| `../adr/0025-api-tiered-access-trusted-purchase-policy.md` | `../adr/0025/0025-api-tiered-access-trusted-purchase-policy.md` |

### `docs/roadmap/payments-atomic-switch.md`

| Zepsuty link (target) | Sugerowana poprawka |
|---|---|
| `../adr/0015-sales-payments-bc-design.md` | `../adr/0015/0015-sales-payments-bc-design.md` |
| `../adr/0024-controller-routing-strategy.md` | `../adr/0024/0024-controller-routing-strategy.md` |
| `../adr/0015-sales-payments-bc-design.md` | `../adr/0015/0015-sales-payments-bc-design.md` |
| `../adr/0024-controller-routing-strategy.md` | `../adr/0024/0024-controller-routing-strategy.md` |

### `docs/roadmap/presale-slice2.md`

| Zepsuty link (target) | Sugerowana poprawka |
|---|---|
| `../adr/0012-presale-checkout-bc-design.md` | `../adr/0012/0012-presale-checkout-bc-design.md` |
| `../adr/0012-presale-checkout-bc-design.md` | `../adr/0012/0012-presale-checkout-bc-design.md` |

## B. Przemianowane pliki .github/instructions/* (18)

### `docs/adr/0002/0002-post-event-storming-architectural-evolution-strategy.md`

| Zepsuty link (target) | Sugerowana poprawka |
|---|---|
| `../../.github/instructions/dotnet-instructions.md` | `../../../.github/instructions/dotnet.instructions.md` |
| `../../.github/instructions/efcore-instructions.md` | `../../../.github/instructions/efcore.instructions.md` |
| `../../.github/instructions/web-api-instructions.md` | `../../../.github/instructions/web-api.instructions.md` |
| `../../.github/instructions/migration-policy.md` | `../../../.github/instructions/migration-policy.instructions.md` |

### `docs/adr/0003/0003-feature-folder-organization-for-new-bounded-context-code.md`

| Zepsuty link (target) | Sugerowana poprawka |
|---|---|
| `../../.github/instructions/dotnet-instructions.md` | `../../../.github/instructions/dotnet.instructions.md` |

### `docs/adr/0004/0004-module-taxonomy-and-bounded-context-grouping.md`

| Zepsuty link (target) | Sugerowana poprawka |
|---|---|
| `../../.github/instructions/dotnet-instructions.md` | `../../../.github/instructions/dotnet.instructions.md` |

### `docs/adr/0006/0006-typedid-and-value-objects-as-shared-domain-primitives.md`

| Zepsuty link (target) | Sugerowana poprawka |
|---|---|
| `../../.github/instructions/dotnet-instructions.md` | `../../../.github/instructions/dotnet.instructions.md` |
| `../../.github/instructions/efcore-instructions.md` | `../../../.github/instructions/efcore.instructions.md` |

### `docs/adr/0008/0008-supporting-currencies-bc-design.md`

| Zepsuty link (target) | Sugerowana poprawka |
|---|---|
| `../../.github/instructions/dotnet-instructions.md` | `../../../.github/instructions/dotnet.instructions.md` |
| `../../.github/instructions/efcore-instructions.md` | `../../../.github/instructions/efcore.instructions.md` |
| `../../.github/instructions/testing-instructions.md` | `../../../.github/instructions/testing.instructions.md` |
| `../../.github/instructions/migration-policy.md` | `../../../.github/instructions/migration-policy.instructions.md` |

### `docs/adr/0010/0010-in-memory-message-broker-for-cross-bc-communication.md`

| Zepsuty link (target) | Sugerowana poprawka |
|---|---|
| `../../.github/instructions/dotnet-instructions.md` | `../../../.github/instructions/dotnet.instructions.md` |

### `docs/adr/0021/0021-frontend-error-pipeline-and-js-migration-strategy.md`

| Zepsuty link (target) | Sugerowana poprawka |
|---|---|
| `../../.github/instructions/frontend-instructions.md` | `../../../.github/instructions/frontend.instructions.md` |

### `docs/architecture/bounded-context-map.md`

| Zepsuty link (target) | Sugerowana poprawka |
|---|---|
| `../../.github/instructions/dotnet-instructions.md` | `../../.github/instructions/dotnet.instructions.md` |

### `docs/patterns/implementation-patterns.md`

| Zepsuty link (target) | Sugerowana poprawka |
|---|---|
| `../../.github/instructions/dotnet-instructions.md` | `../../.github/instructions/dotnet.instructions.md` |

### `docs/roadmap/iam-atomic-switch.md`

| Zepsuty link (target) | Sugerowana poprawka |
|---|---|
| `../../.github/instructions/migration-policy.md` | `../../.github/instructions/migration-policy.instructions.md` |

### `docs/roadmap/orders-atomic-switch.md`

| Zepsuty link (target) | Sugerowana poprawka |
|---|---|
| `../../.github/instructions/migration-policy.md` | `../../.github/instructions/migration-policy.instructions.md` |

## E. Zbedny prefiks "docs/" wewnatrz plikow juz w docs/ (7)

### `docs/specifications/coupons-apply-revert.md`

| Zepsuty link (target) | Sugerowana poprawka |
|---|---|
| `docs/adr/0016/0016-sales-coupons-bc-design.md` | `../adr/0016/0016-sales-coupons-bc-design.md` |

### `docs/specifications/iam-refresh-token.md`

| Zepsuty link (target) | Sugerowana poprawka |
|---|---|
| `docs/adr/0019/README.md` | `../adr/0019/README.md` |
| `docs/roadmap/iam-refresh-token.md` | `../roadmap/iam-refresh-token.md` |

### `docs/specifications/inventory-reservation-release.md`

| Zepsuty link (target) | Sugerowana poprawka |
|---|---|
| `docs/adr/0011/0011-inventory-availability-bc-design.md` | `../adr/0011/0011-inventory-availability-bc-design.md` |

### `docs/specifications/orders-checkout.md`

| Zepsuty link (target) | Sugerowana poprawka |
|---|---|
| `docs/adr/0012/0012-presale-checkout-bc-design.md` | `../adr/0012/0012-presale-checkout-bc-design.md` |
| `docs/adr/0014/0014-sales-orders-bc-design.md` | `../adr/0014/0014-sales-orders-bc-design.md` |

### `docs/specifications/payments-lifecycle.md`

| Zepsuty link (target) | Sugerowana poprawka |
|---|---|
| `docs/adr/0015/0015-sales-payments-bc-design.md` | `../adr/0015/0015-sales-payments-bc-design.md` |

## D. Zle/niepelne sciezki wzgledne (nie zwiazane z restrukturyzacja ADR) (33)

### `docs/adr/0003/0003-feature-folder-organization-for-new-bounded-context-code.md`

| Zepsuty link (target) | Sugerowana poprawka |
|---|---|
| `../architecture/bounded-context-map.md` | `../../architecture/bounded-context-map.md` |

### `docs/adr/0004/0004-module-taxonomy-and-bounded-context-grouping.md`

| Zepsuty link (target) | Sugerowana poprawka |
|---|---|
| `../architecture/bounded-context-map.md` | `../../architecture/bounded-context-map.md` |

### `docs/adr/0008/0008-supporting-currencies-bc-design.md`

| Zepsuty link (target) | Sugerowana poprawka |
|---|---|
| `../architecture/bounded-context-map.md` | `../../architecture/bounded-context-map.md` |

### `docs/adr/0010/0010-in-memory-message-broker-for-cross-bc-communication.md`

| Zepsuty link (target) | Sugerowana poprawka |
|---|---|
| `../architecture/bounded-context-map.md` | `../../architecture/bounded-context-map.md` |

### `docs/adr/0013/0013-per-bc-dbcontext-interfaces.md`

| Zepsuty link (target) | Sugerowana poprawka |
|---|---|
| `../architecture/bounded-context-map.md` | `../../architecture/bounded-context-map.md` |

### `docs/adr/0021/0021-frontend-error-pipeline-and-js-migration-strategy.md`

| Zepsuty link (target) | Sugerowana poprawka |
|---|---|
| `../../ECommerceApp.Application/Middlewares/ExceptionMiddleware.cs` | `../../../ECommerceApp.Application/Middlewares/ExceptionMiddleware.cs` |
| `../../ECommerceApp.Application/Exceptions/ErrorMapToResponse.cs` | `../../../ECommerceApp.Application/Exceptions/ErrorMapToResponse.cs` |
| `../../ECommerceApp.Application/ViewModels/ExceptionResponse.cs` | `../../../ECommerceApp.Application/ViewModels/ExceptionResponse.cs` |
| `../../ECommerceApp.Application/Exceptions/BusinessException.cs` | `../../../ECommerceApp.Application/Exceptions/BusinessException.cs` |
| `../../ECommerceApp.Web/wwwroot/js/errors.js` | `../../../ECommerceApp.Web/wwwroot/js/errors.js` |
| `../../ECommerceApp.Web/wwwroot/js/ajaxRequest.js` | `../../../ECommerceApp.Web/wwwroot/js/ajaxRequest.js` |
| `../../ECommerceApp.Web/wwwroot/js/modalService.js` | `../../../ECommerceApp.Web/wwwroot/js/modalService.js` |

### `docs/adr/0022/0022-navbar-two-tier-redesign.md`

| Zepsuty link (target) | Sugerowana poprawka |
|---|---|
| `../../ECommerceApp.Web/Views/Shared/_Layout.cshtml` | `../../../ECommerceApp.Web/Views/Shared/_Layout.cshtml` |
| `../../ECommerceApp.Application/Inventory/Availability/Services/IStockQueryService.cs` | `../../../ECommerceApp.Application/Inventory/Availability/Services/IStockQueryService.cs` |
| `../../ECommerceApp.Application/Inventory/Availability/Services/StockQueryService.cs` | `../../../ECommerceApp.Application/Inventory/Availability/Services/StockQueryService.cs` |
| `../../ECommerceApp.Web/Controllers/BaseController.cs` | `../../../ECommerceApp.Web/Controllers/BaseController.cs` |
| `../../ECommerceApp.Application/Catalog/Products/Services/ICategoryService.cs` | `../../../ECommerceApp.Application/Catalog/Products/Services/ICategoryService.cs` |

### `docs/adr/0024/0024-controller-routing-strategy.md`

| Zepsuty link (target) | Sugerowana poprawka |
|---|---|
| `../roadmap/orders-atomic-switch.md` | `../../roadmap/orders-atomic-switch.md` |
| `../roadmap/payments-atomic-switch.md` | `../../roadmap/payments-atomic-switch.md` |
| `../architecture/bounded-context-map.md` | `../../architecture/bounded-context-map.md` |

### `docs/adr/0026/0026-order-lifecycle-saga.md`

| Zepsuty link (target) | Sugerowana poprawka |
|---|---|
| `../roadmap/saga-pattern.md` | `../../roadmap/saga-pattern.md` |
| `../roadmap/saga-pattern.md` | `../../roadmap/saga-pattern.md` |

### `docs/rag/mcp-first-routing-migration-playbook.md`

| Zepsuty link (target) | Sugerowana poprawka |
|---|---|
| `mcp-routing.instructions.md` | `../../.github/instructions/mcp-routing.instructions.md` |

### `docs/rag/reports/2026-05-26-architecture-audit-and-refactor.md`

| Zepsuty link (target) | Sugerowana poprawka |
|---|---|
| `tools/rag-dotnet/src/RagTools.Mcp/Middleware/BadRequestEnvelopeMiddleware.cs` | `../../../tools/rag-dotnet/src/RagTools.Mcp/Middleware/BadRequestEnvelopeMiddleware.cs` |
| `tools/rag-dotnet/src/RagTools.Mcp/Middleware/ApiKeyMiddleware.cs` | `../../../tools/rag-dotnet/src/RagTools.Mcp/Middleware/ApiKeyMiddleware.cs` |
| `tools/rag-dotnet/src/RagTools.Mcp/Tools/McpToolGuard.cs` | `../../../tools/rag-dotnet/src/RagTools.Mcp/Tools/McpToolGuard.cs` |
| `tools/rag-dotnet/src/RagTools.Mcp/Tools/RagTools.cs` | `../../../tools/rag-dotnet/src/RagTools.Mcp/Tools/RagTools.cs` |

### `docs/rag/reports/2026-05-26-real-mcp-verification.md`

| Zepsuty link (target) | Sugerowana poprawka |
|---|---|
| `.rag/real_mcp_check.out.txt` | `../../../.rag/real_mcp_check.out.txt` |
| `.rag/compare_servers.out.txt` | `../../../.rag/compare_servers.out.txt` |
| `tools/rag/vuln_probe_results.json` | `../../../tools/rag/vuln_probe_results.json` |
| `tools/rag/vuln_probe_results.json` | `../../../tools/rag/vuln_probe_results.json` |
| `.rag/compare_servers.out.txt` | `../../../.rag/compare_servers.out.txt` |

### `docs/reports/web-ui-views-report.md`

| Zepsuty link (target) | Sugerowana poprawka |
|---|---|
| `endpoint-map.md` | `../reference/endpoint-map.md` |

## C. Odniesienia do skasowanego "context mode" (target nie istnieje - decyzja tresciowa, nie tylko sciezka) (8)

### `docs/playbooks/rag-standalone-global.md`

| Zepsuty link (target) | Sugerowana poprawka |
|---|---|
| `context-mode-bootstrap.md` | `_(brak automatycznej sugestii - patrz zasada 4/5 wyzej)_` |

### `docs/rag/mcp-first-routing-migration-playbook.md`

| Zepsuty link (target) | Sugerowana poprawka |
|---|---|
| `../roadmap/context-mode-integration.md` | `_(brak automatycznej sugestii - patrz zasada 4/5 wyzej)_` |
| `../roadmap/context-mode-integration.md` | `_(brak automatycznej sugestii - patrz zasada 4/5 wyzej)_` |
| `../../.github/skills/rag-with-memory/SKILL.md` | `_(brak automatycznej sugestii - patrz zasada 4/5 wyzej)_` |
| `../roadmap/context-mode-integration.md` | `_(brak automatycznej sugestii - patrz zasada 4/5 wyzej)_` |
| `../roadmap/context-mode-integration.md` | `_(brak automatycznej sugestii - patrz zasada 4/5 wyzej)_` |

### `docs/rag/rag-architecture.md`

| Zepsuty link (target) | Sugerowana poprawka |
|---|---|
| `../roadmap/context-mode-integration.md` | `_(brak automatycznej sugestii - patrz zasada 4/5 wyzej)_` |

### `docs/rag/reports/mcp-routing-eval-50q.md`

| Zepsuty link (target) | Sugerowana poprawka |
|---|---|
| `../../getting-started-context-mode.md` | `_(brak automatycznej sugestii - patrz zasada 4/5 wyzej)_` |

## F. Odniesienia do efemerycznych/skasowanych artefaktow (raporty historyczne) (4)

### `docs/rag/rag-architecture.md`

| Zepsuty link (target) | Sugerowana poprawka |
|---|---|
| `../../tools/rag-dotnet/src/RagTools.Mcp/Tools/RagSession.cs` | `_(brak automatycznej sugestii - patrz zasada 4/5 wyzej)_` |

### `docs/reports/rag-bare-server-integration-2026-05-29.md`

| Zepsuty link (target) | Sugerowana poprawka |
|---|---|
| `../../docker-compose.bare.yaml` | `_(brak automatycznej sugestii - patrz zasada 4/5 wyzej)_` |
| `../../tmp/bare-rag/` | `_(brak automatycznej sugestii - patrz zasada 4/5 wyzej)_` |

### `docs/reports/rag-parity-fix-diagnosis-2026-05-28.md`

| Zepsuty link (target) | Sugerowana poprawka |
|---|---|
| `../../tools/rag-dotnet/src/RagTools.Core/Preprocessors/GlossaryExpansionPreprocessor.cs` | `_(brak automatycznej sugestii - patrz zasada 4/5 wyzej)_` |

---

## Osobny temat: asymetria linkowania docs <-> assets

Nie jest to zepsuty link, tylko brakujacy - `assets/diagrams/README.md` poprawnie linkuje **do**
`docs/specifications/*.md` (5 linkow, dzialaja), ale zaden plik w `docs/specifications/` nie linkuje
z powrotem do swojego diagramu w `assets/diagrams/flows/`. Rekomendacja: dodac w kazdym z 5 plikow
`docs/specifications/*.md` sekcje/link do odpowiadajacego diagramu, np. w `orders-checkout.md`:

```markdown
> Diagram przeplywu: [assets/diagrams/flows/orders-checkout.md](../../assets/diagrams/flows/orders-checkout.md)
```

Mapowanie plik specyfikacji -> plik diagramu (nazwy sie pokrywaja 1:1):

| docs/specifications/ | assets/diagrams/flows/ |
|---|---|
| coupons-apply-revert.md | coupons-apply-revert.md |
| inventory-reservation-release.md | inventory-reservation-release.md |
| orders-checkout.md | orders-checkout.md |
| payments-lifecycle.md | payments-lifecycle.md |
| iam-refresh-token.md | iam-refresh-token.md |

`assets/screens/README.md` to czysta galeria (linkuje tylko do .png), bez linku zwrotnego do docs -
mozna to zostawic, albo dla symetrii dodac na gorze link do `docs/README.md` / `docs/reference/`.

---

## Jak ponownie zwalidowac po poprawkach

Skrypt uzyty do tego audytu (Python 3, bez zaleznosci zewnetrznych) - wystarczy przeskanowac
`docs/**/*.md`, `assets/**/*.md`, `README.md`, wyciagnac linki markdown regexem, odrzucic
`http(s)://` i `mailto:`, rozwiazac `target` wzgledem folderu pliku zrodlowego (`Path(file).parent / target`)
i sprawdzic `Path.exists()`. Poprosze o taki przebieg ponownie po tym, jak Luna wprowadzi poprawki,
zeby potwierdzic 0 zepsutych linkow.
