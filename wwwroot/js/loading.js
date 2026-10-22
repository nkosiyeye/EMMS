// Loading System for EMMS
//
// Shows an overlay while the browser navigates to another page, and guarantees it goes away:
//  - only real same-tab navigations show it (not downloads, new tabs, modifier-clicks, JS-handled links)
//  - pageshow hides it, which also covers pages restored from the back/forward cache
//  - a failsafe timer hides it if the page never unloads (e.g. a file download response)
// Opt a link or form out with data-no-loading.
(function () {
    'use strict';

    const logoPath = '/lib/coa.jpeg';
    const FAILSAFE_MS = 8000;

    const loadingMessages = [
        "Please wait while we load your content",
        "Preparing your dashboard",
        "Fetching latest data",
        "Setting up your workspace",
        "Loading system resources"
    ];

    const loadingCSS = `
        #globalLoadingOverlay {
            opacity: 0;
            visibility: hidden;
            transition: opacity 0.3s ease, visibility 0.3s ease;
        }
        #globalLoadingOverlay.show {
            opacity: 1;
            visibility: visible;
        }
        body.loading {
            overflow: hidden;
        }`;

    const loadingHTML = `
<div id="globalLoadingOverlay" class="position-fixed top-0 start-0 w-100 h-100 d-flex flex-column justify-content-center align-items-center bg-white bg-opacity-75" style="z-index: 9999;" aria-hidden="true">
    <div class="position-relative d-flex justify-content-center align-items-center mb-4">
        <div class="spinner-border text-primary" role="status" style="width: 4rem; height: 4rem;"></div>
        <img src="${logoPath}" alt="" class="position-absolute top-50 start-50 translate-middle rounded-circle shadow" style="width: 3rem; height: 3rem; object-fit: contain;">
    </div>
    <div class="text-center mb-3">
        <h3 class="h5 fw-semibold text-dark mb-1">EMMS</h3>
        <p class="text-muted" id="loadingMessage">${loadingMessages[0]}</p>
    </div>
</div>`;

    let overlay = null;
    let messageEl = null;
    let failsafeTimer = null;
    let messageInterval = null;

    function ensureOverlay() {
        if (overlay) return true;
        if (!document.body) return false;

        const style = document.createElement('style');
        style.textContent = loadingCSS;
        document.head.appendChild(style);

        document.body.insertAdjacentHTML('beforeend', loadingHTML);
        overlay = document.getElementById('globalLoadingOverlay');
        messageEl = document.getElementById('loadingMessage');
        return true;
    }

    function revealMainContent() {
        const mainContent = document.getElementById('main-content');
        if (mainContent) mainContent.style.display = 'flex'; // the app shell is a flex column (style.css)
    }

    function clearTimers() {
        clearTimeout(failsafeTimer);
        clearInterval(messageInterval);
        failsafeTimer = null;
        messageInterval = null;
    }

    function showLoading(customMessage = null, timeout = FAILSAFE_MS) {
        if (!ensureOverlay()) return;
        clearTimers();

        if (customMessage) {
            messageEl.textContent = customMessage;
        } else {
            let messageIndex = 0;
            messageEl.textContent = loadingMessages[messageIndex];
            messageInterval = setInterval(() => {
                messageIndex = (messageIndex + 1) % loadingMessages.length;
                messageEl.textContent = loadingMessages[messageIndex];
            }, 1500);
        }

        document.body.classList.add('loading');
        overlay.classList.add('show');

        // Failsafe: if we're still on this page after the timeout, the navigation either
        // turned into a download or stalled — never leave the user locked out.
        if (timeout > 0) {
            failsafeTimer = setTimeout(hideLoading, timeout);
        }
    }

    function hideLoading() {
        clearTimers();
        revealMainContent();
        if (document.body) document.body.classList.remove('loading');
        if (overlay) overlay.classList.remove('show');
    }

    // True when clicking this link will replace the current page in this tab
    function isSameTabNavigation(link, e) {
        if (e.defaultPrevented) return false;
        if (e.button !== 0 || e.metaKey || e.ctrlKey || e.shiftKey || e.altKey) return false;
        if (link.hasAttribute('data-no-loading') || link.hasAttribute('download')) return false;
        if (link.classList.contains('read-more')) return false;

        const toggle = link.getAttribute('data-bs-toggle');
        if (toggle) return false; // collapse, dropdown, modal, tab, offcanvas...

        const target = (link.getAttribute('target') || '').toLowerCase();
        if (target && target !== '_self') return false;

        const href = link.getAttribute('href');
        if (!href || href.startsWith('#') || /^(javascript|mailto|tel):/i.test(href)) return false;

        let url;
        try { url = new URL(link.href, window.location.href); } catch { return false; }
        if (url.origin !== window.location.origin) return false;

        // Same page, only the hash differs — no navigation happens
        if (url.pathname === window.location.pathname && url.search === window.location.search && url.hash) return false;

        return true;
    }

    // Bubble-phase listener on document so it runs after page handlers and can see preventDefault()
    document.addEventListener('click', function (e) {
        const link = e.target.closest && e.target.closest('a[href]');
        if (!link || !isSameTabNavigation(link, e)) return;

        const linkText = link.textContent.trim().replace(/\s+/g, ' ');
        showLoading(linkText && linkText.length <= 40 ? `Loading ${linkText}...` : null);
    });

    document.addEventListener('submit', function (e) {
        const form = e.target;
        if (e.defaultPrevented || form.hasAttribute('data-no-loading')) return;

        const target = (form.getAttribute('target') || '').toLowerCase();
        if (target && target !== '_self') return;

        // Wait a tick so client-side validation that cancels the submit is respected
        setTimeout(() => {
            if (!e.defaultPrevented) showLoading('Processing request...');
        }, 0);
    });

    // Fires on every page display, including restores from the back/forward cache,
    // where 'load' does not fire and the overlay would otherwise stay up forever.
    window.addEventListener('pageshow', hideLoading);

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', () => { ensureOverlay(); revealMainContent(); });
    } else {
        ensureOverlay();
        revealMainContent();
    }

    window.showLoading = showLoading;
    window.hideLoading = hideLoading;

    window.LoadingSystem = {
        show: showLoading,
        hide: hideLoading,

        // Show the overlay until the promise settles
        forAjax: function (promise, message = 'Processing request...') {
            showLoading(message, 0);
            if (promise && typeof promise.finally === 'function') {
                return promise.finally(hideLoading);
            }
            hideLoading();
            return promise;
        },

        withDelay: function (callback, delay = 1000, message = 'Processing...') {
            showLoading(message, 0);
            setTimeout(() => {
                hideLoading();
                if (typeof callback === 'function') callback();
            }, delay);
        }
    };
})();
