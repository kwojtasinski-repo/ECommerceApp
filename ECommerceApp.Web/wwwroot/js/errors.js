define([],
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
            }

            function containsParams(value) {
                return /[^{[]+(?=\})/.test(value);
            }

            function buildParamsObject(args) {
                if (!args) {
                    return null;
                }

                const paramsString = args.split(',');
                if (paramsString.length == 0) {
                    return null;
                }

                const params = {};
                for (const param of paramsString) {
                    const splited = param.split('=');
                    if (splited.length < 2) {
                        continue;
                    }
                    const name = splited[0];
                    const value = splited[1];
                    params[name] = value;
                }
                return params;
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

                    const params = buildParamsObject(args);
                    if (!params) {
                        return value;
                    }

                    for (const paramName in params) {
                        const regexPattern = new RegExp('\{' + paramName + '\}');
                        if (!regexPattern.test(value)) {
                            continue;
                        }
                        value = value.replace(regexPattern, params[paramName]);
                    }
                    return value;
                },
                getErrorNew: function (key, args) {
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
                        debugger
                        const regexPattern = new RegExp('\{' + param.Name + '\}');
                        if (!regexPattern.test(value)) {
                            continue;
                        }
                        value = value.replace(regexPattern, param.Value);
                    }
                    return value;
                }
            }
        })();


        function getErrorText(error) {
            if (error.status === 400) {
                if (!error.responseJSON.ErrorCode) {
                    return error.responseJSON.Error;
                }

                return error.responseJSON.ErrorCode;
            } else if (response.status === 404) {
                return resourceNotFound;
            } else {
                return generalError;
            }
        }

        function redirectToError(errorText, ...args) {
            const urlParams = new URLSearchParams(window.location.search);
            urlParams.set('Error', errorText);
            if (!args) {
                window.location.href = window.location.origin + window.location.pathname + '?' + urlParams.toString();
                return;
            }

            let params = '';
            for (const arg of args) {
                for (const a in arg) {
                    params += a + '=' + arg[a] + ',';
                }
            }

            if (!params) {
                window.location.href = window.location.origin + window.location.pathname + '?' + urlParams.toString();
                return;
            }

            params = params.substring(params.lastIndexOf(','), 0);
            urlParams.set('Params', params);
            window.location.href = window.location.origin + window.location.pathname + '?' + urlParams.toString();
        }

        return {
            errors,
            getErrorText,
            redirectToError
        };
    }
);
