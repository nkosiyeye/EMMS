// Loading System for CBHIS Admin
(function () {
    'use strict';
    const logoPath = '/lib/coa.jpeg';
    // Create loading overlay HTML
    const loadingHTML = `<!-- Global Loading Overlay -->
<div id="globalLoadingOverlay" class="position-fixed top-0 start-0 w-100 h-100 d-flex flex-column justify-content-center align-items-center bg-white bg-opacity-75" style="z-index: 9999;">
    
    <!-- Spinner with Logo -->
    <div class="position-relative d-flex justify-content-center align-items-center mb-4">
        <!-- Spinner -->
        <div class="spinner-border text-primary" role="status" style="width: 4rem; height: 4rem;"></div>

        <!-- Logo in center -->
        <img src="${logoPath}" alt="CBHIS" class="position-absolute top-50 start-50 translate-middle rounded-circle shadow" style="width: 3rem; height: 3rem; object-fit: contain;">
    </div>

    <!-- Loading Text -->
    <div class="text-center mb-3">
        <h3 class="h5 fw-semibold text-dark mb-1">EMMS</h3>
        <p class="text-muted" id="loadingMessage">Please wait while we load your content</p>
    </div>

</div>


    `;

    // Add CSS for the loading animation
    const loadingCSS = `
        <style>
            @keyframes loadingProgress {
                0% { width: 0%; }
                50% { width: 70%; }
                100% { width: 100%; }
            }
            
            #globalLoadingOverlay {
                opacity: 0;
                visibility: hidden;
                transition: opacity 0.3s ease, visibility 0.3s ease;
            }
            
            #globalLoadingOverlay.show {
                opacity: 1;
                visibility: visible;
            }
            
            .loading-fade-in {
                animation: loadingFadeIn 0.3s ease-in-out;
            }
            
            @keyframes loadingFadeIn {
                from { opacity: 0; transform: scale(0.95); }
                to { opacity: 1; transform: scale(1); }
            }
            
            /* Prevent page interaction while loading */
            body.loading {
                overflow: hidden;
            }
        </style>
    `;

    // Loading messages array for variety
    const loadingMessages = [
        "Please wait while we load your content",
        "Preparing your dashboard",
        "Fetching latest data",
        "Setting up your workspace",
        "Loading system resources"
    ];

    let loadingTimeout;
    let messageInterval;

    // Initialize loading system
    function initLoadingSystem() {
        // Add CSS to head
        document.head.insertAdjacentHTML('beforeend', loadingCSS);

        // Add loading overlay to body
        document.body.insertAdjacentHTML('beforeend', loadingHTML);

        // Get loading overlay element
        const loadingOverlay = document.getElementById('globalLoadingOverlay');
        const loadingMessageEl = document.getElementById('loadingMessage');

        // Show loading function
        window.showLoading = function (customMessage = null, timeout = 1000000) {
            if (customMessage) {
                loadingMessageEl.textContent = customMessage;
            } else {
                // Rotate through different messages
                let messageIndex = 0;
                loadingMessageEl.textContent = loadingMessages[messageIndex];

                messageInterval = setInterval(() => {
                    messageIndex = (messageIndex + 1) % loadingMessages.length;
                    loadingMessageEl.textContent = loadingMessages[messageIndex];
                }, 1500);
            }

            document.body.classList.add('loading');
            loadingOverlay.classList.add('show', 'loading-fade-in');

            // Auto-hide after timeout (failsafe)
            //loadingTimeout = setTimeout(() => {
            //    hideLoading();
            //}, timeout);
        };

        // Hide loading function
        window.hideLoading = function () {
            clearTimeout(loadingTimeout);
            clearInterval(messageInterval);

            const loadingOverlay = document.getElementById('globalLoadingOverlay');
            const loadingMessageEl = document.getElementById('loadingMessage');

            if (loadingOverlay) {
                loadingOverlay.classList.remove('show');
            }
            document.body.classList.remove('loading');

            // Show the main content
            const mainContent = document.getElementById('main-content');
            if (mainContent) {
                mainContent.style.display = 'block';
            }

            // Reset message and fade-out overlay
            setTimeout(() => {
                if (loadingOverlay) {
                    loadingMessageEl.textContent = loadingMessages[0];
                    loadingOverlay.classList.remove('loading-fade-in');
                }
            }, 300);
        };


        // Add event listeners for navigation links
        attachNavigationListeners();
    }

    // Attach listeners to navigation links
    function attachNavigationListeners() {
        const navLinks = document.querySelectorAll('a, .nav-link');

        navLinks.forEach(link => {
            link.addEventListener('click', function (e) {
                // Don't show loading for current page
                if (this.classList.contains('active')) return;

                // Skip collapse or dropdown toggles
                const toggle = this.getAttribute('data-bs-toggle');
                if (toggle === 'collapse' || toggle === 'dropdown') return;

                // Optional: Skip links with only '#' href
                if (this.getAttribute('href') === '#') return;

                // Show loading with navigation-specific message
                const linkText = this.textContent.trim();
                showLoading(`Loading ${linkText}...`);
            });
        });
    }


    // Attach listeners to forms

    // Handle page unload (when navigating away)
    window.addEventListener('beforeunload', function () {
        showLoading('Navigating...', 5000);
    });

    // Handle page load complete
    //window.addEventListener('load', function () {
    //    hideLoading();
    //});

    // Handle DOM content loaded (for faster hiding)
    window.addEventListener('load', function () {
        setTimeout(() => {
            hideLoading();
        }, 200); // tiny delay for smoother transition
    });

    // Handle back/forward browser navigation
    //window.addEventListener('popstate', function () {
    //    showLoading('Loading page...', 3000);
    //});

    // Initialize when DOM is ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initLoadingSystem);
    } else {
        initLoadingSystem();
    }

    // Expose manual control functions globally
    window.LoadingSystem = {
        show: window.showLoading,
        hide: window.hideLoading,

        // Utility function for AJAX requests
        forAjax: function (promise, message = 'Processing request...') {
            showLoading(message);

            if (promise && typeof promise.then === 'function') {
                promise.finally(() => {
                    hideLoading();
                });
            }

            return promise;
        },

        // Utility function for delayed actions
        withDelay: function (callback, delay = 1000, message = 'Processing...') {
            showLoading(message);

            setTimeout(() => {
                hideLoading();
                if (typeof callback === 'function') {
                    callback();
                }
            }, delay);
        }
    };

})();