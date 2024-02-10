define([],
    function () {
        "use strict";

        /**
        * Contains operations with form validators.
        * Every validator should contain formId which is id of form, object to validate
        * Optional validator can contain method beforeSubmit which can be used for specific use case, can also contain method validate which is custom validation of validator and is used before submit
        * Stucture of validator { controlId: 'idForm', objectToValidate: { ... } }
        * Stucture of validator with method beforeSubmit and validate { controlId: 'idForm', objectToValidate: { ... }, beforeSubmit: function() { ... }, validate: function() { ... } }
        * Object to validate should contain fields: controlId - id name of html element, rules - array of function to validate, valid - define if field is valid, value - value of field, optional onChange - method invoked insted of default onChange method for specific resons
        * Example of object stucture { controlId: 'idObject', rules: [ v => v || 'Value is required' ] , valid: false, value: '' }
        * Stucture with optional method onChange { controlId: 'idObject', rules: [ v => v || 'Value is required' ] , valid: false, value: '', onChange: function(event) { ... } }
        *
        */
        const forms = (function () {
            /**
            * Iterate Through object and set rules listener which can handle operations
            * @param obj Object to invoke.
            *
            */
            function iterateThroughObjectAndSetRulesListeners(obj) {
                iterateThroughObjectAndRunCallbackOnField(obj, setRulesListeners);
            }

            /**
            * Set Rules Listeners onchange event depends on controlId, controlName
            * @param field Html Header of modal.
            *
            */
            function setRulesListeners(field) {
                document.addEventListener("change", function (event) {
                    if (event.target.id === field.controlId || event.target.name === field.controlName) {
                        if (typeof field.onChange === 'function') {
                            field.onChange(event.target);
                            return;
                        }
                        validOnChange(event.target, field);
                    }
                });
            }

            /**
            * Invoked when field is changed
            * @param context Context value for example html element.
            * @param field Field of validator.
            *
            */
            function validOnChange(context, field) {
                field.value = context.value;
                for (const rule of field.rules) {
                    const message = rule(context.value);
                    if (message && message.length > 0) {
                        field.valid = false;
                        showError(context, message);
                        return;
                    }
                }

                field.valid = true;
                $(context).siblings('span').text('');
            }

            /**
            * Creates Error span html
            * @param text Text inside error span.
            *
            */
            function createErrorSpanInner(text) {
                const span = document.createElement('span');
                span.className = "text-danger field-validation-invalid";
                span.textContent = text;
                return span;
            }

            /**
            * Shows error siblings to closest context html
            * @param context Html element.
            * @param text Text of error.
            *
            */
            function showError(context, text) {
                const siblingSpan = $(context).siblings('span');
                if (siblingSpan[0]) {
                    siblingSpan.text(text);
                } else {
                    $('#' + context.attributes.id.value)[0].parentElement.appendChild(createErrorSpanInner(text));
                }
            }

            /**
            * Validates form
            * @param validator Form Validator to validate.
            *
            */
            function validateForm(validator) {
                if (typeof validator.validate === 'function') {
                    return validator.validate();
                }
                return validAllFields(validator);
            }

            /**
            * Validates all validators fields
            * @param obj Validator.
            *
            */
            function validAllFields(obj) {
                iterateThroughObjectAndRunCallbackOnField(obj, validField);
                try {
                    iterateThroughObjectAndRunCallbackOnField(obj, throwIfFieldIsInvalidAndScrollToControl);
                    return true;
                } catch {
                    return false;
                }
            }

            /**
            * Method throws error if validation fails and scrolls to closest error
            * @param field Invalid validator's Field.
            *
            */
            function throwIfFieldIsInvalidAndScrollToControl(field) {
                if (!field.valid) {
                    $('#' + field.controlId)[0].scrollIntoView({ behavior: 'smooth', block: 'center' });
                    throw new Error('Field ' + field.controlId + ' has invalid field');
                }
            }

            /**
            * Method validates field
            * @param field Field of validator.
            *
            */
            function validField(field) {
                for (const rule of field.rules) {
                    const val = rule(field.value);
                    if (val && val.length > 0) {
                        field.valid = false;
                        forms.showValidationError(field.controlId, val);
                        return false;
                    }
                }
                return true;
            }

            /**
            * Clear all validation messages for all fields
            * @param obj Validator.
            *
            */
            function clearValidationMessagesForAllFields(obj) {
                iterateThroughObjectAndRunCallbackOnField(obj, clearValidationMessageForField);
            }

            /**
            * Clears validation messages for specific field
            * @param field Field of validator.
            *
            */
            function clearValidationMessageForField(field) {
                field.valid = true;
                forms.showValidationError(field.controlId, '');
            }
            /**
             * Initialize form fields
             * @param {any} form FormValidator
             */
            function initializeFormFields(form) {
                if (typeof form.beforeInitializeFields === 'function') {
                    form.beforeInitializeFields();
                }
                iterateThroughObjectAndRunCallbackOnField(form, getValueFromHtml);
                function getValueFromHtml(field) {
                    if (typeof field.onInitializeField === 'function') {
                        field.onInitializeField(field);
                        return;
                    }
                    const value = $('#' + field.controlId).val();
                    if (!value) {
                        return;
                    }

                    field.value = value;
                }
            }

            /**
            * Iterate through object and run callback if object contains form field
            * @param obj Validator.
            * @param callback method to invoke with field argument.
            *
            */
            function iterateThroughObjectAndRunCallbackOnField(obj, callback) {
                for (const field in obj) {
                    if (typeof obj[field] !== 'object' || obj[field] === null) {
                        continue;
                    }

                    if (Array.isArray(obj[field].rules) && (typeof obj[field].controlId === 'string' || obj[field].controlId instanceof String)) {
                        callback(obj[field]);
                        continue;
                    }

                    iterateThroughObjectAndRunCallbackOnField(obj[field], callback);
                }
            }

            return {
                /**
                * Intialize form validator, set rules, actions before submit
                * @param formValidator Validator.
                *
                */
                initFormValidator: function (formValidator) {
                    if (typeof formValidator !== 'object' || formValidator === null) {
                        return;
                    }
                    iterateThroughObjectAndSetRulesListeners(formValidator);
                    initializeFormFields(formValidator);
                    $('#' + formValidator.formId).submit(function (event) {
                        event.preventDefault();
                        clearValidationMessagesForAllFields(formValidator);
                        if (!validateForm(formValidator)) {
                            return;
                        }
                        if (typeof formValidator.beforeSubmit === 'function') {
                            formValidator.beforeSubmit();
                        }
                        $(this).unbind('submit').submit(); // continue the submit unbind preventDefault
                    });
                },
                /**
                * Shows validations errors depends on controlId
                * @param controlId Control id.
                * @param text Text to show.
                *
                */
                showValidationError: function (controlId, text) {
                    if (!controlId) {
                        return;
                    }
                    const siblingSpan = $('#' + controlId).siblings('span');
                    if (siblingSpan[0]) {
                        siblingSpan.text(text);
                    } else {
                        $('#' + controlId)[0].parentElement.appendChild(forms.createErrorSpan(text));
                    }
                },
                /**
                * Creates html error span
                * @param text Text of error.
                *
                */
                createErrorSpan: function (text) {
                    return createErrorSpanInner(text);
                },
                /**
                * Validate control
                * @param controlField Validator's field.
                *
                */
                validControl: function (controlField) {
                    validField(controlField);
                },
                /**
                * Clear Validation error
                * @param controlId id of control (html id attribute).
                *
                */
                clearValidationError(controlId) {
                    forms.showValidationError(controlId, '');
                }
            }
        })();
        return {
            forms
        }
    }
);