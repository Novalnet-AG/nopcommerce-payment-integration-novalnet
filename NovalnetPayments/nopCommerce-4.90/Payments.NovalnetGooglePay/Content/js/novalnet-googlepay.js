var NovalnetGooglePay = (function ($) {

    function init(config) {

        var isCheckoutPage = config.page == 'checkout';
        var isProductPage = config.page == 'product';
        var isCartPage = config.page == 'cart';

        var logPrefix = isCheckoutPage ? '[NN GooglePay]' : '[NN GooglePay Express]';
        var GooglePayJsonData = config.jsonData;
        var productId = config.productId || null;

        //keeps track of the last address the wallet reported, so onShippingMethodChange
        //can recalculate totals for it without the buyer re-selecting an address
        var lastKnownShippingAddress = null;

        //country the payment intent is built with: the shop's default (customer's address on file, else the
        //store's default country) until the buyer picks an address in the Google Pay sheet, then that country
        var countryCode = GooglePayJsonData.default_country_code || 'DE';

        var googlepayPaymentObj = null;

        if (isCartPage) {
            //the widget zone this button is rendered into (order_summary_content_before) sits above the
            //cart table, but the button should appear directly above the checkout buttons instead
            var $checkoutButtons = $('.checkout-buttons').first();
            if ($checkoutButtons.length) {
                $(config.container).insertBefore($checkoutButtons);
            }

            document.addEventListener('click', function (e) {
                var googlePayButton = e.target.closest('#nn-googlepay-express-container-cart button');

                if (!googlePayButton) {
                    return;
                }
                
                var $terms = $('#termsofservice');
                if ($terms.length && !$terms.is(':checked')) {
                    e.preventDefault();
                    e.stopPropagation();
                    e.stopImmediatePropagation();
                    $('#terms-of-service-warning-box').dialog();
                    return false;
                }
            }, true);
        }

        //checkout only: nopCommerce clears the selected payment method whenever the cart changes (e.g. in
        //another tab) or checkout restarts, and OpcSavePaymentInfo then fails with "Payment method is not
        //selected" - confirm checkout is still valid, otherwise restart it so the buyer reviews the new cart
        //returns a promise that resolves to true when the payment can still be submitted, false otherwise
        function verifyCheckoutState() {
            return $.post(config.checkoutStateUrl).then(function (state) {
                if (state.valid) {
                    return true;
                }
                alert(state.message);
                location.reload();
                return false;
            }, function (jqXHR, textStatus, errorThrown) {
                return false;
            });
        }

        //express only: current cart (or product) amount and line items
        function fetchTransactionInfo() {
            return $.post(config.transactionInfoUrl, { productId: productId })
                .done(function (response) {
                })
                .fail(function (jqXHR, textStatus, errorThrown) {
                    console.log(logPrefix, 'fetchTransactionInfo: failed', textStatus, errorThrown);
                });
        }

        //asks the shop (real shipping providers, real cart) for shipping options and
        //line items for a given (partial) address, instead of using hardcoded values
        function fetchShippingUpdate(address, selectedOptionId) {
            var requestData = {
                city: (address && address.city) || '',
                state: (address && address.state) || '',
                country: (address && address.country) || '',
                postalCode: (address && address.postalCode) || '',
                selectedOptionId: selectedOptionId || '',
                pageType: config.page
            };
            
            return $.post(config.updateShippingUrl, requestData)
                .done(function (response) {
                })
                .fail(function (jqXHR, textStatus, errorThrown) {
                    console.log(logPrefix, 'fetchShippingUpdate: failed', textStatus, errorThrown);
                });
        }

        function toShippingMethods(response) {
            var methods = (response.shippingOptions || []).map(function (option) {
                return {
                    identifier: option.identifier,
                    amount: option.amount,
                    detail: option.detail,
                    label: option.label
                };
            });

            //the sheet shows/pre-selects whichever method is first in this array, so the currently selected
            //shipping method (response.defaultIdentifier) needs to be moved to the front rather than left
            //wherever the backend happened to list it
            if (response.defaultIdentifier) {
                var selectedIndex = methods.findIndex(function (method) {
                    return method.identifier === response.defaultIdentifier;
                });
                if (selectedIndex > 0) {
                    var selectedMethod = methods.splice(selectedIndex, 1)[0];
                    methods.unshift(selectedMethod);
                }
            }

            return methods;
        }


        //initial data the payment sheet is built from
        function fetchInitialData() {
            if (isCheckoutPage) {
                if(config.isVirtualProduct != 1) {
                    var customer = GooglePayJsonData.customer || {};
                    var shipping = customer.shipping || {};
                    var billing = customer.billing || {};
                    var source = (shipping.same_as_billing !== 1 && shipping.zip) ? shipping : billing;

                    var initialAddress =  {
                        city: source.city || '',
                        state: source.state || '',
                        country: source.country_code || '',
                        postalCode: source.zip || ''
                    };
                    return fetchShippingUpdate(initialAddress);
                }
            }
            return fetchTransactionInfo();
        }

        function buildPaymentIntent(data) {
            var lang = ((GooglePayJsonData.custom && GooglePayJsonData.custom.lang) || 'en').toLowerCase();
            var buttonInformation = {
                type: GooglePayJsonData.button.type,
                locale: lang === 'de' ? 'de-DE' : 'en-US',
                dimensions: { height: GooglePayJsonData.button.height, width: "auto" },
                boxSizing: 'fill'
            };

            var orderInformation = {
                merchantName: GooglePayJsonData.business_name,
                lineItems: data.lineItems,
                billing: {requiredFields: ['postalAddress', 'phone', 'email']}
            };

            var callbacks = {
                onProcessCompletion: onProcessCompletion,
                onPaymentButtonClicked: onPaymentButtonClicked
            };

            if(config.isVirtualProduct != 1 && !isCheckoutPage) {
                callbacks.onShippingContactChange = onShippingContactChange;
                callbacks.onShippingMethodChange = onShippingMethodChange;
                orderInformation.shipping = {
                    requiredFields: ['postalAddress', 'phone', 'email'],
                    methodsUpdatedLater: true
                };
            }

            var paymentIntent = {
                clientKey: GooglePayJsonData.client_key,
                paymentIntent: {
                    merchant: {
                        countryCode: countryCode,
                        partnerId: GooglePayJsonData.google_merchant_id
                    },
                    transaction: {
                        setPendingPayment: true,
                        amount: data.amount,
                        currency: GooglePayJsonData.transaction.currency,
                        paymentMethod: 'GOOGLEPAY',
                        environment: GooglePayJsonData.transaction.test_mode ? 'SANDBOX' : 'PRODUCTION'
                    },
                    order: orderInformation,
                    button: buttonInformation,
                    callbacks: callbacks
                }
            };
            return paymentIntent;
        }

        //checkout: make sure the payment can still be submitted before the sheet opens
        //express: the cart must hold the right items before the sheet opens
        function onPaymentButtonClicked(done) {
            if (isCheckoutPage) {
                verifyCheckoutState().then(function (isValid) {
                    done(isValid ? {} : { status: 'FAILURE' });
                });
                return;
            }

            function openSheetWithTransactionInfo(txInfo) {
                if (txInfo.error) {
                    displayBarNotification(txInfo.error, 'error', 0);
                    done({ status: 'FAILURE' });
                    return;
                }
                googlepayPaymentObj.setPaymentIntent(buildPaymentIntent(txInfo));
                done({});
            }

            function onTransactionInfoFailed() {
                displayBarNotification('Unable to calculate the order total', 'error', 0);
                done({ status: 'FAILURE' });
            }

            if (isProductPage) {
                var form = $('#product-details-form');
                if (form.length === 0) {
                    displayBarNotification('Unable to add product to cart', 'error', 0);
                    done({ status: 'FAILURE' });
                    return;
                }

                if (config.checkoutDisabled == 1) {
                    displayBarNotification(config.checkoutDisabledMessage, 'error', 0);
                    done({ status: 'FAILURE' });
                    return;
                }

                fetchTransactionInfo().done(function (txInfo) {
                    if (txInfo.error || txInfo.productInCart) {
                        //the product is already in the cart from a previous click on this button (the sheet
                        //was closed last time without completing payment) - only add it to the cart the first
                        //time, otherwise every retry would add another unit
                        openSheetWithTransactionInfo(txInfo);
                        return;
                    }

                    $.ajax({
                        async: false,
                        type: 'POST',
                        url: config.addToCartUrl,
                        data: form.serialize(),
                        success: function (response) {
                            if (!response.success && !response.redirect) {
                                if (response.message) {
                                    displayBarNotification(response.message, 'error', 0);
                                }
                                done({ status: 'FAILURE' });
                                return;
                            }
                           openSheetWithTransactionInfo(txInfo);
                           //fetchTransactionInfo().done(openSheetWithTransactionInfo).fail(onTransactionInfoFailed);
                        },
                        error: function (jqXHR, textStatus, errorThrown) {
                            displayBarNotification('Unable to add product to cart', 'error', 0);
                            done({ status: 'FAILURE' });
                        }
                    });
                }).fail(onTransactionInfoFailed);
            } else if (isCartPage) {
                if ($('#termsofservice').length > 0 && !$('#termsofservice').is(':checked')) {
                    $('#terms-of-service-warning-box').dialog();
                    done({ status: 'FAILURE' });
                    return;
                }

                if (config.checkoutDisabled == 1) {
                    displayBarNotification(config.checkoutDisabledMessage, 'error', 0);
                    done({ status: 'FAILURE' });
                    return;
                }

                fetchTransactionInfo().done(openSheetWithTransactionInfo).fail(onTransactionInfoFailed);
            } else {
                done({});
            }
        }

        function onProcessCompletion(response, bookingResult) {
			
            if (!response.result || response.result.status !== 'SUCCESS') {
                bookingResult({ status: 'FAILURE', statusText: '' });
                return;
            }

            var transaction = response.transaction || {};
            var paymentData = transaction.paymentData || {};

            if (isCheckoutPage) {
                verifyCheckoutState().then(function (isValid) {
                    if (!isValid) {
                        bookingResult({
                            status: 'FAILURE',
                            statusText: ''
                        });
                        return;
                    }

                    var $tokenField = $(config.tokenField);
                    var $redirectField = $(config.doRedirectField);

                    // Set Novalnet payment values.
                    $tokenField.val(transaction.token);
                    $redirectField.val(transaction.doRedirect ? '1' : '0');

                    // One-page checkout.
                    if (typeof PaymentInfo !== 'undefined' && typeof PaymentInfo.save === 'function') {
                        bookingResult({status: 'SUCCESS', statusText: ''});

                        PaymentInfo.save();
                        return;
                    }

                    // Multi-step checkout: find the normal Payment Info form.
                    var $form = $('form[action="/checkout/paymentinfo"]').filter(
                        function () {
                            return $(this).find('.payment-info-next-step-button').length > 0;
                        }
                    ).first();

                    if (!$form.length) {
                        // Fallback to locating the form through its Next button.
                        $form = $('.payment-info-next-step-button').first().closest('form');
                    }

                    if (!$form.length) {
                        console.error('Novalnet Google Pay: Payment Info form not found.');

                        bookingResult({status: 'FAILURE', statusText: 'Payment form not found.'});
                        return;
                    }

                    // Ensure payment fields are included in the form POST.
                    [$tokenField, $redirectField].forEach(function ($field) {
                        if ($field.length && !$.contains($form[0], $field[0])) {
                            $form.append($field);
                        }
                    });

                    var $nextButton = $form.find('button.payment-info-next-step-button[name="nextstep"]').first();

                    if (!$nextButton.length) {
                        bookingResult({status: 'FAILURE', statusText: 'Payment submit button not found.'});
                        return;
                    }

                    bookingResult({status: 'SUCCESS', statusText: ''});

                    $nextButton.trigger('click');

                }).catch(function (error) {
                    bookingResult({status: 'FAILURE', statusText: ''});
                });

                return;
            }

            var postData = {
                nnGooglePayResponse: JSON.stringify({
                    token: transaction.token,
                    doRedirect: transaction.doRedirect ? '1' : '0',
                    order: response.order,
                    response_test: response
                })
            };
            //PlaceExpressOrder is covered by the controller's [AutoValidateAntiforgeryToken]
            addAntiForgeryToken(postData);

            $.post(config.placeOrderUrl, postData)
                .done(function (data) {
                    if (data.error) {
                        displayBarNotification(data.error, 'error', 0);
                        bookingResult({ status: 'FAILURE', statusText: data.error });
                        return;
                    }

                    bookingResult({ status: 'SUCCESS', statusText: '' });

                    if (data.redirectUrl) {
                        window.location.href = data.redirectUrl;
                        return;
                    }

                    setLocation(data.redirect);
                })
                .fail(function (jqXHR, textStatus, errorThrown) {
                    displayBarNotification('Unable to place the order', 'error', 0);
                    bookingResult({ status: 'FAILURE', statusText: '' });
                });
        }

        function onShippingContactChange(shippingContact, newShippingContactResult) {

            var address = {
                city: shippingContact.locality || shippingContact.city || '',
                state: shippingContact.administrativeArea || shippingContact.state || '',
                country: shippingContact.countryCode || shippingContact.country || '',
                postalCode: shippingContact.postalCode || shippingContact.zip || ''
            };
            lastKnownShippingAddress = address;

            if (address.country) {
                countryCode = address.country.toUpperCase();
            }

            fetchShippingUpdate(address).done(function (response) {
                if (response.error) {
                    newShippingContactResult({ error: response.error });
                    return;
                }

                newShippingContactResult({
                    amount: response.amount,
                    methods: toShippingMethods(response),
                    lineItems: response.lineItems
                });
            }).fail(function () {
                newShippingContactResult({ error: 'Unable to calculate shipping for this address' });
            });
        }

        function onShippingMethodChange(shippingMethod, newShippingMethodResult) {
			
            fetchShippingUpdate(lastKnownShippingAddress, shippingMethod.identifier).done(function (response) {
                if (response.error) {
                    newShippingMethodResult({ error: response.error });
                    return;
                }

                newShippingMethodResult({
                    amount: response.amount,
                    lineItems: response.lineItems
                });
            }).fail(function () {
                newShippingMethodResult({ error: 'Unable to recalculate totals' });
            });
        }

        try {
            var NovalnetPaymentInstance = NovalnetPayment();
            googlepayPaymentObj = NovalnetPaymentInstance.createPaymentObject();

            if (isCheckoutPage) {
                $('.payment-info-next-step-button').hide();

                $(document).off('ajaxComplete.nnGooglePay').on('ajaxComplete.nnGooglePay', function () {
                    if (!$(config.container).length) {
                        $('.payment-info-next-step-button').show();
                        $(document).off('ajaxComplete.nnGooglePay');
                    }
                });
            }

            fetchInitialData().done(function (data) {
                if (data.error) {
                    return;
                }

                googlepayPaymentObj.setPaymentIntent(buildPaymentIntent(data));

                googlepayPaymentObj.isPaymentMethodAvailable(function (displayGooglePayButton) {
                    if (displayGooglePayButton) {
                        googlepayPaymentObj.addPaymentButton(config.container);
                    }
                });
            }).fail(function () {
                console.log('Unable to load Google Pay for this page');
            });
        }
    }

    return {
        init: init
    };
})(jQuery);
