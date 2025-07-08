/**
 * Centrny Internationalization (i18n) JavaScript Helper
 * Provides client-side localization support
 */

window.CentrinyI18n = (function() {
    'use strict';

    // Current culture information
    let currentCulture = 'en-US';
    let isRTL = false;

    // Initialize i18n on page load
    function init() {
        // Detect current culture from HTML lang attribute or cookie
        const htmlLang = document.documentElement.lang || 'en-US';
        currentCulture = htmlLang;
        isRTL = htmlLang === 'ar-EG';
        
        // Set RTL direction
        updateDirection();
        
        // Update any dynamic content that needs localization
        updateDynamicContent();
        
        console.log('Centrny i18n initialized:', { culture: currentCulture, isRTL: isRTL });
    }

    // Update HTML direction attribute
    function updateDirection() {
        if (isRTL) {
            document.documentElement.setAttribute('dir', 'rtl');
            document.body.classList.add('rtl');
        } else {
            document.documentElement.setAttribute('dir', 'ltr');
            document.body.classList.remove('rtl');
        }
    }

    // Update dynamic content based on culture
    function updateDynamicContent() {
        // Update date/time formats in existing content
        updateDateTimeFormats();
        
        // Update number formats
        updateNumberFormats();
        
        // Update any data attributes that contain localizable content
        updateDataAttributes();
    }

    // Format dates according to current culture
    function formatDate(date, options = {}) {
        const defaultOptions = {
            year: 'numeric',
            month: 'long',
            day: 'numeric'
        };
        
        const formatOptions = { ...defaultOptions, ...options };
        
        try {
            return new Intl.DateTimeFormat(currentCulture, formatOptions).format(date);
        } catch (e) {
            console.warn('Date formatting failed, falling back to default:', e);
            return date.toLocaleDateString();
        }
    }

    // Format time according to current culture
    function formatTime(date, options = {}) {
        const defaultOptions = {
            hour: '2-digit',
            minute: '2-digit'
        };
        
        const formatOptions = { ...defaultOptions, ...options };
        
        try {
            return new Intl.DateTimeFormat(currentCulture, formatOptions).format(date);
        } catch (e) {
            console.warn('Time formatting failed, falling back to default:', e);
            return date.toLocaleTimeString();
        }
    }

    // Format numbers according to current culture
    function formatNumber(number, options = {}) {
        try {
            return new Intl.NumberFormat(currentCulture, options).format(number);
        } catch (e) {
            console.warn('Number formatting failed, falling back to default:', e);
            return number.toString();
        }
    }

    // Format currency according to current culture
    function formatCurrency(amount, currency = 'EGP') {
        try {
            return new Intl.NumberFormat(currentCulture, {
                style: 'currency',
                currency: currency
            }).format(amount);
        } catch (e) {
            console.warn('Currency formatting failed, falling back to default:', e);
            return `${amount} ${currency}`;
        }
    }

    // Update existing date/time elements
    function updateDateTimeFormats() {
        // Update elements with data-date attribute
        document.querySelectorAll('[data-date]').forEach(element => {
            const dateValue = element.getAttribute('data-date');
            if (dateValue) {
                const date = new Date(dateValue);
                if (!isNaN(date.getTime())) {
                    element.textContent = formatDate(date);
                }
            }
        });

        // Update elements with data-time attribute
        document.querySelectorAll('[data-time]').forEach(element => {
            const timeValue = element.getAttribute('data-time');
            if (timeValue) {
                const date = new Date(timeValue);
                if (!isNaN(date.getTime())) {
                    element.textContent = formatTime(date);
                }
            }
        });
    }

    // Update number format elements
    function updateNumberFormats() {
        document.querySelectorAll('[data-number]').forEach(element => {
            const numberValue = element.getAttribute('data-number');
            if (numberValue) {
                const number = parseFloat(numberValue);
                if (!isNaN(number)) {
                    element.textContent = formatNumber(number);
                }
            }
        });

        document.querySelectorAll('[data-currency]').forEach(element => {
            const currencyValue = element.getAttribute('data-currency');
            const currency = element.getAttribute('data-currency-code') || 'EGP';
            if (currencyValue) {
                const amount = parseFloat(currencyValue);
                if (!isNaN(amount)) {
                    element.textContent = formatCurrency(amount, currency);
                }
            }
        });
    }

    // Update data attributes with localized content
    function updateDataAttributes() {
        // Update title attributes for tooltips
        document.querySelectorAll('[data-title-key]').forEach(element => {
            const titleKey = element.getAttribute('data-title-key');
            // This would be populated from server-side localized data if needed
            // For now, we'll just ensure RTL compatibility
            if (isRTL && element.title) {
                element.setAttribute('dir', 'rtl');
            }
        });
    }

    // AJAX request wrapper that includes culture information
    function ajaxRequest(url, options = {}) {
        const defaultOptions = {
            headers: {
                'Accept-Language': currentCulture,
                'X-Requested-With': 'XMLHttpRequest'
            }
        };

        // Merge options
        const requestOptions = {
            ...defaultOptions,
            ...options,
            headers: {
                ...defaultOptions.headers,
                ...(options.headers || {})
            }
        };

        return fetch(url, requestOptions);
    }

    // Dynamic content loading with localization
    function loadLocalizedContent(containerId, url, params = {}) {
        const container = document.getElementById(containerId);
        if (!container) {
            console.error('Container not found:', containerId);
            return Promise.reject('Container not found');
        }

        // Add culture to parameters
        const localizedParams = {
            ...params,
            culture: currentCulture
        };

        const queryString = new URLSearchParams(localizedParams).toString();
        const requestUrl = `${url}?${queryString}`;

        return ajaxRequest(requestUrl)
            .then(response => response.text())
            .then(html => {
                container.innerHTML = html;
                // Re-initialize i18n for new content
                updateDynamicContent();
                return html;
            })
            .catch(error => {
                console.error('Failed to load localized content:', error);
                throw error;
            });
    }

    // Public API
    return {
        init: init,
        getCurrentCulture: () => currentCulture,
        isRTL: () => isRTL,
        formatDate: formatDate,
        formatTime: formatTime,
        formatNumber: formatNumber,
        formatCurrency: formatCurrency,
        updateDynamicContent: updateDynamicContent,
        ajaxRequest: ajaxRequest,
        loadLocalizedContent: loadLocalizedContent
    };
})();

// Auto-initialize when DOM is ready
document.addEventListener('DOMContentLoaded', function() {
    window.CentrinyI18n.init();
});

// Re-initialize on language change (if content is dynamically updated)
document.addEventListener('languageChanged', function() {
    window.CentrinyI18n.init();
});