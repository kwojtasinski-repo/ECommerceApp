# UI/UX: jak było (2024) vs jak jest (2026) i co poprawić

> Raport porównawczy + lista propozycji. Nic tu nie jest jeszcze wdrożone.
> Data: 2026-10-22. Cel: strona ma być przyjemna w użyciu — **większe obrazki, większa czcionka, mniej szumu**.

## 1. Skąd porównanie

| Stan | Źródło | Uwagi |
|---|---|---|
| **„Kiedyś"** (luty 2024) | 39 plików `screen_*.PNG` z roota repo (commit `4f0d18e6`, „Updated images") + `schemat_bazy.png` | Widok sprzed migracji na Bootstrap 5 i sprzed podziału na Areas. Zostają w root na czas dalszej analizy kierunku (roadmapa O8), docelowo do usunięcia — wtedy nadal będą w historii gita. |
| **Teraz** | `assets/screens/web` (16) + `assets/screens/backoffice` (8), czerwiec 2026 | Uwaga: **te screeny są starsze niż paleta** z 2026-08-25 (`68200ff4`). Na screenach przyciski są jeszcze niebieskie, a w CSS `.btn-primary` to już bursztyn `--accent`. Zanim cokolwiek z tego raportu zostanie porównane „na oko", trzeba zrobić świeże screeny. |
| Reguły | [`colors.md`](../style-guides/colors.md), [`buttons.md`](../style-guides/buttons.md), `ECommerceApp.Web/wwwroot/css/site.css` | |

Obejrzałem komplet starych i obecnych screenów (kontaktówki), nie każdy w pełnej rozdzielczości.

## 2. Porównanie: co się zmieniło

| Obszar | Kiedyś (2024) | Teraz (2026) | Ocena |
|---|---|---|---|
| **Nawigacja** | Jeden rząd: duże ikony (Home, Przedmioty, Koszyk, Moje zamówienia, Dane kontaktowe, Zamówienia, Kupony, Zarządzanie użytkownikami) z podpisami pod spodem; ikony ~35 px | Pasek z wyszukiwarką + filtr kategorii + koszyk + konto, pod nim drugi rząd z menu domenowym (Kupony / Katalog / Magazyn / Realizacja / Zaplecze / Backoffice) | Lepsza struktura i wyszukiwarka. Ale ikony i tekst są dużo mniejsze, a menu domenowe jest widoczne dla każdego (patrz 3.4). |
| **Strona główna** | Pusta strona: tytuł, jedno zdanie, jeden przycisk | Hero „Witaj w ECommerceApp" + sekcja „Najnowsze oferty" z kartą produktu | Duży krok naprzód, ale hero jest płaskie, a karty małe. |
| **Lista produktów** | Tabela: Id, Nazwa, Koszt, **Opis (długi tekst techniczny)**, Gwarancja, Ilość, BrandId, TypeId, Waluta | Dwa widoki: karty z obrazkiem (Storefront) i czysta tabela (Nazwa / Cena / Status / Akcje) | Zdecydowanie lepiej. Znikły surowe Id i opis w tabeli. |
| **Obrazki produktu** | Miniatury ~100 px w szczegółach, bez zdjęcia na liście | Karta: obraz 180 px wysokości, `object-fit: cover`; szczegóły: `max-height:150px` | Są, ale nadal małe (patrz 3.1). |
| **Tabele** | Dużo kolumn z surowymi Id (`Id użytego kuponu`, `Id zamawiającego`, `RefundId`), GUID-y zamówień, linki `Edytuj \| Szczegóły \| Usuń` w jednej linii | Czytelniejsze nagłówki, badge statusu (Opublikowany / Szkic / Dostarczono), przyciski zamiast linków w części widoków | Dobry kierunek, ale niekonsekwentny: Waluty, Zamówienia, Kupony (backoffice) nadal mają linki tekstowe i „Id". |
| **Formularze** | Ciasne, pola o różnej szerokości, brak grupowania; `Realizacja zamówienia` to długi pionowy ciąg pól | Dalej głównie jedna kolumna; `PlaceOrder.cshtml` używa `col-md-*` w wielu proporcjach (2/3/4/5/6/8) | Prawie bez zmian. To największy dług. |
| **Statusy** | Checkboxy `Czy dostarczono`, `Czy zapłacono` (zablokowane) | Badge i tekst statusu (`Fulfilled`, `PaymentConfirmed`, `Confirmed`) | Lepiej, ale statusy są po angielsku i bez koloru w tabelach zamówień/płatności. |
| **Komunikaty / modale** | Modale „Tak / Nie" przy usuwaniu | Bez zmian + puste stany (`Twój koszyk jest pusty` z CTA „Przeglądaj oferty") | Puste stany to wyraźny plus. |
| **Kolory** | Domyślny niebieski Bootstrapa `#1b6ec2`, białe tło | Teal `--brand` + bursztyn `--accent` (jedyny CTA), ciepłe tło `--paper` | Spójna tożsamość, opisana w style guide. |
| **Czcionka** | Baza 14 px / 16 px desktop | **Bez zmian** (`html { font-size: 14px }`, 16 px od 768 px). Tekst w tabelach i na kartach wygląda jeszcze mniejszy, bo jest `.small` lub `table-sm` | **Bez poprawy w tym, o co prosili ludzie.** |

**Podsumowanie:** struktura i spójność wizualna poszły mocno do przodu. Dwie rzeczy, o które prosili użytkownicy — **większe obrazki i większa czcionka** — praktycznie nie zostały zrobione. Bootstrapowy szkielet jest dobry, więc większość poniższych poprawek to CSS i kilka widoków, nie przebudowa.

## 3. Co zrobić, żeby strona była przyjemna

Priorytety: **P1** = największy efekt za najmniejszy koszt, **P2** = wyraźna poprawa, **P3** = polerowanie. Rozmiary: XS < 1 h, S ≈ pół dnia, M ≈ dzień+.

### 3.1 Obrazki (to, o co prosili ludzie)

| # | Propozycja | Pri | Rozmiar |
|---|---|---|---|
| I1 | **Karta produktu: obraz w proporcji 4:3 lub 1:1 zamiast stałych 180 px.** Użyć `ratio ratio-4x3` + `object-fit: cover`, wtedy obraz rośnie razem z kartą. Wynosić styl z atrybutu `style=""` do klasy `.product-card-img` w `site.css`. | P1 | XS |
| I2 | **Karty szersze:** zmienić `row-cols-lg-4` na `row-cols-lg-3 row-cols-xl-4` i zdjąć wąski limit — na screenach pojedyncza karta zajmuje ok. 1/4 szerokości i wygląda na zagubioną. | P1 | XS |
| I3 | **Strona szczegółów produktu: duża galeria.** Główne zdjęcie po lewej (≥ 50% szerokości kolumny), miniatury pod spodem, kliknięcie otwiera powiększenie (modal Bootstrapa, bez nowej biblioteki). Dziś: `img-thumbnail` z `max-height:150px`. | P1 | S |
| I4 | **Obrazek w koszyku i na liście pozycji zamówienia** (miniatura 64–80 px). Dziś koszyk i zamówienie to same teksty, a użytkownik rozpoznaje produkt po obrazku. | P2 | S |
| I5 | **Placeholder, gdy produkt nie ma zdjęcia** (neutralna ikona na tle `--paper`), żeby karty miały równą wysokość. | P2 | XS |
| I6 | `loading="lazy"` i `width`/`height` na obrazkach (brak skoków układu przy ładowaniu). | P3 | XS |

### 3.2 Czcionka i czytelność

| # | Propozycja | Pri | Rozmiar |
|---|---|---|---|
| T1 | **Podnieść bazę do 16 px na wszystkich szerokościach** (usunąć regułę 14 px dla mobile). 14 px na telefonie to poniżej zalecenia WCAG dla komfortowego czytania. | P1 | XS |
| T2 | **Skalować tylko widoki klienckie** (storefront, koszyk, checkout, moje zamówienia, moje płatności) do `1.0625–1.125rem` dla treści, nie ruszając gęstych tabel backoffice. `colors.md` już ostrzega, że globalna zmiana może rozbić tabele admina — dlatego klasa-wrapper (np. `.shop-ui`) na `<main>` w layoucie klienckim, nie zmiana `html`. | P1 | S |
| T3 | **Hierarchia nagłówków:** `h1` strony 2rem+, nagłówki sekcji 1.35rem, cena na karcie pogrubiona i większa niż nazwa (dziś nazwa i cena wyglądają podobnie). | P1 | XS |
| T4 | **Kontrast tekstu pomocniczego:** `text-muted` na `--paper` bywa za jasny (np. „Łącznie: 3", cena na karcie). Przyciemnić do min. 4.5:1 (EAA / WCAG 2.1 AA, które `colors.md` już przywołuje). | P2 | XS |
| T5 | **Line-height 1.5–1.6 dla tekstu opisów** (opisy produktów są długie i dziś ściśnięte). | P3 | XS |

### 3.3 Tabele i formularze

| # | Propozycja | Pri | Rozmiar |
|---|---|---|---|
| F1 | **Dla klienta zamiast tabel — karty** w „Moje zamówienia" i „Moje płatności": numer, data, kwota, badge statusu, jeden przycisk „Szczegóły". Tabela zostaje w backoffice. Na telefonie tabela z 7 kolumnami jest nieużywalna (przewijana tabel jest tylko w 6 plikach `.cshtml` — `table-responsive`). | P1 | M |
| F2 | **Kolorowe badge statusów + polskie etykiety** (`Fulfilled → Zrealizowane`, `PaymentConfirmed → Opłacone`, `Placed → Złożone`, `Confirmed → Potwierdzona`). Mapowanie w jednym miejscu (np. istniejące resource/enum extensions), nie `switch` w każdym widoku. Zgodne z decyzją o i18n: to są teksty domenowe. | P1 | S |
| F3 | **Ujednolicić akcje w wierszu:** wszędzie przyciski wg [`buttons.md`](../style-guides/buttons.md) (Szczegóły outline, Edytuj caution, Usuń danger-outline), koniec z `Edytuj \| Szczegóły \| Usuń` w Walutach, Zamówieniach i Kuponach. | P2 | S |
| F4 | **Ukryć kolumny techniczne** (`Id`, `Id klienta`, `Id waluty`) w widokach klienta; w backoffice zostawić, ale na końcu i szarym kolorem. | P2 | S |
| F5 | **Formularz zamówienia (`PlaceOrder`) w dwóch kolumnach:** po lewej dane i adres, po prawej sticky podsumowanie (lista pozycji, kod promocyjny, kwota, przycisk). Kroki jako prosty pasek (Koszyk → Dane → Płatność). Dziś to jedna długa kolumna z 8 różnymi układami `col-md-*`. | P1 | M |
| F6 | **`form-floating` lub etykiety nad polami o stałej szerokości** + `autocomplete` (`given-name`, `street-address`, `postal-code`) — mniej pisania w checkoutcie. | P2 | S |
| F7 | **Walidacja inline** (komunikat pod polem, nie tylko podsumowanie u góry) i zachowanie wpisanych danych po błędzie. | P2 | S |

### 3.4 Nawigacja i układ

| # | Propozycja | Pri | Rozmiar |
|---|---|---|---|
| N1 | **Menu domenowe tylko dla ról, które z niego korzystają.** Gość i zwykły klient widzą dziś „Kupony / Katalog / Magazyn / Realizacja / Zaplecze / Backoffice" (screeny pokazują je na stronie głównej). Klient powinien widzieć: Oferty, Kategorie, Moje zamówienia/płatności/zwroty. Zasady autoryzacji już istnieją po stronie serwera — to tylko kwestia renderowania menu. | P1 | S |
| N2 | **Powiększyć cele kliknięcia** (min. 44×44 px dla ikon koszyka/konta; EAA) i dodać licznik pozycji przy koszyku (w 2024 był, dziś nie widać). | P1 | XS |
| N3 | **Breadcrumbs** w katalogu i szczegółach produktu (Oferty › Smartphone › Xiaomi Redmi 3S). | P2 | S |
| N4 | **Ograniczyć szerokość treści** (`container` ~1140–1320 px) i wycentrować — na szerokich monitorach tabele rozciągają się na cały ekran, a strony typu „Szczegóły" są puste po prawej. | P2 | XS |
| N5 | **Stopka:** dodać linki (kontakt, regulamin, polityka prywatności) i przestać używać `position: absolute` z `line-height: 60px` — na krótkich stronach stopka „wisi" w losowym miejscu (widać na screenach). | P3 | S |
| N6 | **Responsywność:** sprawdzić całą ścieżkę zakupową na 360–414 px (najpierw koszyk i checkout). Dziś brak screenów mobilnych w `assets/screens/`. | P1 | S |

### 3.5 Ścieżka zakupowa (UX, nie tylko wygląd)

| # | Propozycja | Pri | Rozmiar |
|---|---|---|---|
| U1 | **Dostępność produktu widoczna na karcie** (badge „Dostępny / Ostatnie sztuki / Brak") — dane z magazynu już są (`Dostępne` w przeglądzie stanów). Zgodne z zasadą „status natychmiast". | P1 | S |
| U2 | **Mini-koszyk po „Do koszyka"** (toast z miniaturą + „Przejdź do koszyka") zamiast cichego dodania; `cartNotification.js` już istnieje. | P2 | S |
| U3 | **Filtr i sortowanie na liście ofert:** sortuj wg ceny/nowości, zakres ceny. Dziś są tylko chipy tagów i wyszukiwarka. | P2 | M |
| U4 | **Skeleton / spinner przy ładowaniu list** i na przyciskach akcji (zapobiega podwójnemu kliknięciu przy płatności). | P3 | S |
| U5 | **Śledzenie przesyłki jako oś czasu** (Złożone → Opłacone → Wysłane → Dostarczone) na stronie zamówienia; dziś `Historia zdarzeń` to tabela dwóch kolumn. | P3 | M |

## 4. Proponowana kolejność

1. **Paczka „szybkie wygrane" (jedna sesja, głównie `site.css` + 3 widoki):** I1, I2, I5, T1, T3, T4, N2, N4.
2. **Paczka „klient":** I3, I4, T2, F1, F2, N1, U1.
3. **Paczka „checkout":** F5, F6, F7, U2, N6.
4. **Polerowanie:** reszta P2/P3.

Wszystko przez tokeny z `site.css` i klasy z `buttons.md` — bez nowych literałów kolorów i bez nowych klas `.btn-*`. Nowe reguły (karta produktu, `.shop-ui`, badge statusów) warto dopisać jako osobne style guides w `docs/style-guides/` (np. `cards.md`, `typography.md`), zgodnie z zapowiedzią w `docs/style-guides/README.md`.

## 5. Czego nie wiem / do sprawdzenia

- **Świeże screeny.** Porównanie opiera się na screenach z czerwca 2026, sprzed palety teal/bursztyn. Przed wdrożeniem warto zrobić nowy komplet (w tym mobilny) i wymienić `assets/screens/`.
- **Czy „duże obrazki" to prośba o większą kartę, czy o galerię na stronie szczegółów?** Raport zakłada oba (I1–I3). Jeśli ma być jedno, I3 ma największy efekt na decyzję o zakupie.
- **Czy admin ma zostać gęsty?** Raport zakłada tak (T2 omija backoffice). Jeśli i tam ma być większa czcionka, trzeba najpierw przejrzeć 45 widoków z tabelami.
- Nie uruchamiałem aplikacji ani nie mierzyłem kontrastu narzędziem — uwagi o kontraście (T4) są z oceny wizualnej screenów.
