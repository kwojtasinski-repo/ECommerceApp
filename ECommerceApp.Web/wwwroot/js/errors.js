﻿define([],
    function () {
        "use strict";

        const errors = (function () {
            const values = {
                generalError: 'Przepraszamy, ale coś poszło nie tak. Spróbuj ponownie później',
                resourceNotFound: 'Nie znaleziono zasobu',
                addressDeletePolicy: 'Nie możesz usunąć adresu, jeśli masz tylko 1',
                addressNotFound: 'Nie znaleziono adres o id {id}',
                contactDetailDeletePolicy: 'Nie możesz usunąć informacji kontaktowych, jeśli masz tylko 1',
                contactDetailNotFound: 'Nie znaleziono danych kontaktowych o id {id}',
                customerNotExists: 'Dane kontaktowe nie istnieją, sprawdź czy używasz poprawnych',
                contactDetailTypeNotFound: 'Nie znaleziono typu danych kontaktowych o id {id}',
                couponNotFoundByPromo: 'Kupon o nazwie {promoCode} nie został znaleziony',
                couponWasAssigned: 'Kupon został już przypisany',
                couponCannotBeDeleted: 'Nie można usunąć użytego kuponu o id {id}',
                couponInvalidDiscount: 'Obniżka jest niepoprawna. Wartość powinna być w przedziale od 1 do 99',
                couponCodeAlreadyExists: 'Kupon o naziwe {code} już istnieje',
                currencyCodeEmpty: 'Kod waluty nie może być pusty',
                cannotDeleteCouponFromPaidOrder: 'Nie można usunąć kupon z zapłaconego zamówienia',
                orderNotExistsWhileAddCoupon: 'Nie można dodać kupon do nieistniejącego zamówienia',
                addCouponToPaidOrderNotAllowed: 'Nie można dodać kuponu do zapłaconego zamówienia',
                changePaymentOnOrderNotAllowed: 'Nie można zmienić płatności na zamówieniu, na początek odepnij aktualną płatność a potem dodaj nową',
                currencyNotFound: 'Nie znaleziono waluty o id {id}',
                customerNotFound: 'Nie znaleziono danych o id {id}',
                brandNotFound: 'Nie znaleziono firmy o id {id}',
                typeNotFound: 'Nie znaleziono typu o id {id}',
                itemNotFound: 'Nie znaleziono przedmiotu o id {id}',
                positionNotFoundInCart: 'Sprawdź czy pozycja o id {id} jest w koszyku',
                positionInCartNotFound: 'Pozycja o id {id} nie została znaleziona',
                refundNotAssignedToAnyOrder: 'Zwrot o id {id} nie został przypisany do jakiegokolwiek zamówienia',
                orderNotFound: 'Zamówienie o id {id} nie zostało znalezione',
                couponUsedNotFound: 'Kupon użyty o id {id} nie został znaleziony',
                couponConnectWithOrderNotFound: 'Kupon użyty o id {id} połączony z zamówieniem o id {orderId} nie został znaleziony',
                couponNotFound: 'Kupon o id {id} nie został znaleziony',
                orderNotFoundCheckIfPaidDelivered: 'Opłacone zamówienie o id {id} nie zostało znalezione, sprawdź czy zostało lub czy nie zostało już dostarczone',
                couponWithCodeNotFound: 'Kupon z kodem {code} nie został znaleziony',
                existedCouponAssignNotAllowed: 'Nie można przypisać użyty kupon o id {id}',
                orderAlreadyPaid: 'Zamówienie o id {id} zostało już opłacone',
                payForOrderPaymentNotAllowed: 'Nie można zapłacić za już zapłacone zamówienie o id {id}',
                assignExistedPaymentNotAllowed: 'Nie można przypisać istniejącą już płatność o id {id}',
                paymentAlreadyPaid: 'Płatność o id {id} została już opłacona',
                paymentNotFound: 'Nie znaleziono płatności o id {id}',
                customerNotRelatedWithOrder: 'Żadne dane kontaktowe nie zostały powiązane z zamówieniem o id {id}',
                userNotFound: 'Użytkownik o id {id} nie został znaleziony',
                itemNotInStock: 'Przedmiot o id {id} i nazwie {name} nie jest dostępny',
                tooManyItemsQuantityInCart: 'Ilość sztuk przedmiotu o id {id} i nazwie {name} została przekroczona, maksymalnie można zamówić {availableQuantity}',
                tagNotFound: 'Nie znaleziono taga o id {id}',
                invalidImageFormat: 'Niepoprawny format obrazka o nazwie {name}',
                imageNotFound: 'Obrazek o id {id} nie został znaleziony',
                allowedNumberImagesExceeded: 'Liczba obrazków nie może przekroczyć 5',
                orderNotOrderedByCurrentUser: 'Przedmiot o id {id} nie został zamówiony przez aktualnego użytkownika',
                orderItemNotFoundOnOrder: 'Pozycja o id {id} nie została znaleziona na zamówieniu',
                fileSizeTooBig: 'Rozmiar {size} pliku {name} jest za duży, dozwolony rozmiar {allowedSize} bajtów',
                fileExtensionNotAllowed: 'Niepoprawne rozszerzenie {extension} pliku {name}, dozwolone rozszerzenia {extensions}',
                tooManyImages: 'Nie można dodać więcej niż {allowedImagesCount} obrazków. Aktualnie dodano {imageCount} dla przedmiotu o id {id}',
                couponTypeNotFound: 'Nie znaleziono użytego kuponu o id {id}'
            }

            function containsParams(value) {
                return /[^{[]+(?=\})/.test(value);
            }

            return {
                getError: function (key, args) {
                    if (!key) {
                        return '';
                    }

                    let value = values[key] ?? key;
                    if (!args) {
                        return value;
                    }

                    if (value === key) {
                        return value;
                    }

                    if (!containsParams(value)) {
                        return value;
                    }
                    
                    if (args.length == 0) {
                        return value;
                    }

                    for (const param of args) {
                        const regexPattern = new RegExp('\{' + (param.Name ?? param.name) + '\}');
                        if (!regexPattern.test(value)) {
                            continue;
                        }
                        value = value.replace(regexPattern, param.Value ?? param.value);
                    }
                    return value;
                }
            }
        })();

        function showErrorFromResponse(error) {
            if (!error.responseJSON) {
                return;
            }

            showError(error.responseJSON);
        }

        function showError(errorsArray) {
            const errorContainer = document.querySelector('#ErrorContainer');
            const errorValue = document.querySelector('#ErrorValue');
            if (!errorContainer || !errorValue) {
                return;
            }

            if (!errorsArray || !errorsArray.length || errorsArray.length === 0) {
                errorContainer.style.display = 'none';
                errorValue.textContent = '';
                return;
            }

            errorContainer.style.display = 'block';
            let text = '';
            for (const error of errorsArray) {
                text += errors.getError(error.Code ?? error.code, error.Parameters ?? error.parameters) + '\n';
            }
            errorValue.textContent = text;
        }
        
        return {
            errors,
            showErrorFromResponse,
            showError
        };
    }
);
