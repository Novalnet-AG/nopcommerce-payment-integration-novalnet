var NovalnetApplePay = (function ($) {

    function addButtonStyles() {
        if (document.getElementById('nn-applepay-styles')) {
            return;
        }

        $('<style id="nn-applepay-styles">' +
            '.nn-applepay-container, .nn-applepay-express-container { width: 100%; max-width: 100%; box-sizing: border-box; margin: 10px 0; }' +
            'apple-pay-button { display: block; width: 100%; }' +
            '</style>').appendTo('head');
    }

    function init(config) {

        addButtonStyles();

        var isCheckoutPage = config.page === 'checkout';
        var isProductPage = config.page === 'product';
        var isCartPage = config.page === 'cart';
        
        var ApplePayJsonData = config.jsonData;
        var productId = config.productId || null;

        var lastKnownShippingAddress = null;
        var productQueuedForCart = false;
        var productAlreadyInCart = false;

        var applepayPaymentObj = null;

        if (isCartPage) {
            //the widget zone this button is rendered into (order_summary_content_before) sits above the
            //cart table, but the button should appear directly above the checkout buttons instead
            var $checkoutButtons = $('.checkout-buttons').first();
            if ($checkoutButtons.length) {
                $(config.container).insertBefore($checkoutButtons);
            }
        }

        //express only: current cart (or product) amount and line items
        function fetchTransactionInfo() {
            return $.post(config.transactionInfoUrl, { productId: productId })
                .done(function (response) {
                })
                .fail(function (jqXHR, textStatus, errorThrown) {
                    console.log('fetchTransactionInfo: failed', textStatus, errorThrown);
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
                    var customer = ApplePayJsonData.customer || {};
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
            var countryCode = ApplePayJsonData.default_country_code || 'DE';

            var merchantInformation = {
                countryCode: countryCode,
            };

            var transactionInformation = {
                setPendingPayment: true,
                amount: data.amount,
                currency: ApplePayJsonData.transaction.currency,
                paymentMethod: 'APPLEPAY',
                environment: ApplePayJsonData.transaction.test_mode ? 'SANDBOX' : 'PRODUCTION'
            };

            //on the product/cart pages no shipping address has been picked yet (GetExpressTransactionInfo
            //never returns shippingOptions), so the sheet must open with no upfront methods/defaultIdentifier
            //and rely purely on methodsUpdatedLater + onShippingContactChange to populate them later
            var shippingInformation = {
                requiredFields: ['postalAddress', 'phone', 'email'],
                methodsUpdatedLater: true
            };

            if (isCheckoutPage) {
                shippingInformation.requiredFields = ['postalAddress', 'phone'];
                shippingInformation.methods = toShippingMethods(data);
                shippingInformation.defaultIdentifier = data.defaultIdentifier;
            }

            var orderInformation = {
                merchantName: ApplePayJsonData.business_name,
                lineItems: data.lineItems,
                billing: {
                    requiredFields: ['postalAddress', 'phone', 'email']
                },
                shipping: {
                    requiredFields: ['email'],
                    methodsUpdatedLater: true
                },
            };

             var callbacks = {
                onProcessCompletion: onProcessCompletion,
                onPaymentButtonClicked: onPaymentButtonClicked
            };

            //checkout: the sheet can open straight away, nothing to prepare    
            if(config.isVirtualProduct != 1 && !isCheckoutPage) {
                callbacks.onShippingContactChange = onShippingContactChange;
                callbacks.onShippingMethodChange = onShippingMethodChange;
                orderInformation.shipping = {
                    requiredFields: ['postalAddress', 'phone', 'email'],
                    methodsUpdatedLater: true
                };
            }
            

            //the Novalnet SDK's APPLEPAY button config: type/style take the admin-configured values as-is
            //(no translation), cornerRadius lives inside dimensions (not as a sibling of type/style) -
            //passing it at the wrong level is what triggers the SDK's internal APPLEPAY lookup to blow up
            var lang = ((ApplePayJsonData.custom && ApplePayJsonData.custom.lang) || 'en').toLowerCase();
            var buttonInformation = {
                type: ApplePayJsonData.button_type,
                style: ApplePayJsonData.button_theme,
                locale: lang === 'de' ? 'de-DE' : 'en-US',
                boxSizing: 'border-box',
                dimensions: {
                    width: 'auto',
                    height: ApplePayJsonData.button_height || 45,
                    cornerRadius: ApplePayJsonData.button_corner_radius || 0
                }
            };

            var paymentIntent = {
                clientKey: ApplePayJsonData.client_key,
                paymentIntent: {
                    merchant: merchantInformation,
                    transaction: transactionInformation,
                    order: orderInformation,
                    button: buttonInformation,
                    callbacks: callbacks
                }
            };
            
            return paymentIntent;
        }

        //express only: the cart must hold the right items before the order is placed. done() is always
        //called synchronously (see productQueuedForCart), using the payment intent (product price or cart
        //total) that was already set when the button was rendered - any cart mutation happens in the background
        function onPaymentButtonClicked(done) {
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

                if (!productAlreadyInCart && !productQueuedForCart) {
                    productQueuedForCart = true;
                    $.ajax({
                        type: 'POST',
                        url: config.addToCartUrl,
                        data: form.serialize(),
                        success: function (response) {
                            if (response.success) {
                                productAlreadyInCart = true;
                            } else {
                                //let a later click retry the add-to-cart
                                productQueuedForCart = false;
                                if (!response.redirect && response.message) {
                                    displayBarNotification(response.message, 'error', 0);
                                }
                            }
                        },
                        error: function (jqXHR, textStatus, errorThrown) {
                            productQueuedForCart = false;
                        }
                    });
                }
                done({});
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

                done({});
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

            if (isCheckoutPage) {
                var $tokenField = $(config.tokenField);
                $tokenField.val(transaction.token);

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
                    bookingResult({status: 'FAILURE', statusText: 'Payment form not found.'});
                    return;
                }

                // Ensure payment fields are included in the form POST.
                if ($tokenField.length && !$.contains($form[0], $tokenField)) {
                    $form.append($tokenField);
                }
                
                var $nextButton = $form.find('button.payment-info-next-step-button[name="nextstep"]').first();

                if (!$nextButton.length) {
                    bookingResult({status: 'FAILURE', statusText: 'Payment submit button not found.'});
                    return;
                }

                bookingResult({status: 'SUCCESS', statusText: ''});

                // Trigger the normal form submission.
                $nextButton.trigger('click');
                return;
            }

            var postData = {
                nnApplePayResponse: JSON.stringify({
                    token: transaction.token,
                    order: response.order
                })
            };
            addAntiForgeryToken(postData);

            $.post(config.placeOrderUrl, postData)
                .done(function (data) {

                    if (data.error) {
                        displayBarNotification(data.error, 'error', 0);
                        bookingResult({ status: 'FAILURE', statusText: data.error });
                        return;
                    }

                    bookingResult({ status: 'SUCCESS', statusText: '' });
                    setLocation(data.redirect);
                })
                .fail(function (jqXHR, textStatus, errorThrown) {
                    displayBarNotification('Unable to place the order', 'error', 0);
                    bookingResult({ status: 'FAILURE', statusText: '' });
                });
        }

        function onShippingContactChange(shippingContact, newShippingContactResult) {
            //field names follow the Apple Pay shipping contact shape
            var address = {
                city: shippingContact.locality || shippingContact.city || '',
                state: shippingContact.administrativeArea || shippingContact.state || '',
                country: shippingContact.countryCode || shippingContact.country || '',
                postalCode: shippingContact.postalCode || shippingContact.zip || ''
            };
            lastKnownShippingAddress = address;

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
            applepayPaymentObj = NovalnetPaymentInstance.createPaymentObject();

            if (isCheckoutPage) {
                //the Apple Pay button replaces the regular "Continue" button of the Payment Info step
                $('.payment-info-next-step-button').hide();

                //the button lives in the one page checkout shell, not in this view, so it stays hidden when the
                //buyer goes back and picks another payment method - restore it once the Payment Info step is
                //reloaded (via ajax) without the Apple Pay container
                $(document).off('ajaxComplete.nnApplePay').on('ajaxComplete.nnApplePay', function () {
                    if (!$(config.container).length) {
                        $('.payment-info-next-step-button').show();
                        $(document).off('ajaxComplete.nnApplePay');
                    }
                });
            }

            fetchInitialData().done(function (data) {
                if (data.error) {
                    return;
                }

                if (!isCheckoutPage) {
                    productAlreadyInCart = data.productInCart;
                }

                applepayPaymentObj.setPaymentIntent(buildPaymentIntent(data));

                applepayPaymentObj.isPaymentMethodAvailable(function (displayApplePayButton) {
                    if (displayApplePayButton) {
                        applepayPaymentObj.addPaymentButton(config.container);
                    }
                });
            }).fail(function () {
               displayBarNotification('Unable to load Apple Pay for this page');
            });
        }
    }

    return {
        init: init
    };
})(jQuery);
