# sources-raw: lokalne kopie źródeł (NIE w repozytorium zdalnym)

W tym folderze lokalnie leży 43 pełne teksty cudzych artykułów (posty bradmurry.com wklejone przez użytkownika albo pobrane `curl`em, strony dokumentacji AutoGen). **Nie są commitowane ani wypychane do publicznego repozytorium**, bo to pełne kopie cudzych treści.

Jak odtworzyć proweniencję bez tych plików:
- [`../sources-index.md`](../sources-index.md) wskazuje dla każdego pliku URL lub opis źródła, informację czy był w pierwotnej liście 18 URL oraz sposób pozyskania.
- Findings w [`../findings/`](../findings/) zawierają krótkie cytaty (≤ 40 słów) z podaniem źródła; cytaty weryfikowano skryptem względem tych lokalnych plików (wynik w `../01-cross-source-verification.md` §5).
- Pliki są dostępne wyłącznie na komputerze, na którym research wykonano. Jeśli potrzebujesz ich w repozytorium, decyzja o publikacji należy do właściciela repozytorium (prawa autorskie, licencje).
