// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

// Antiforgery: the server validates a token on every POST (AutoValidateAntiforgeryToken in Program.cs).
// The layouts put the token in <meta name="csrf-token">; these hooks attach it to every same-origin POST.
function emmsCsrfToken() {
    return document.querySelector('meta[name="csrf-token"]')?.content ?? '';
}

function emmsIsUnsafeSameOrigin(method, url) {
    return !/^(GET|HEAD|OPTIONS)$/i.test(method || 'GET')
        && new URL(url, window.location.href).origin === window.location.origin;
}

// Adds the hidden token field to a POST form that lacks one (forms built in JS or with a literal action=)
function emmsAddCsrfInput(form) {
    if (form.method.toLowerCase() !== 'post' || form.querySelector('input[name="__RequestVerificationToken"]')) return;
    const input = document.createElement('input');
    input.type = 'hidden';
    input.name = '__RequestVerificationToken';
    input.value = emmsCsrfToken();
    form.appendChild(input);
}

// Capture phase so the field is in place before the browser serialises the form
document.addEventListener('submit', event => emmsAddCsrfInput(event.target), true);

if (window.jQuery) {
    $(document).ajaxSend((event, xhr, settings) => {
        if (emmsIsUnsafeSameOrigin(settings.type, settings.url)) xhr.setRequestHeader('RequestVerificationToken', emmsCsrfToken());
    });
}

const emmsNativeFetch = window.fetch.bind(window);
window.fetch = (resource, init = {}) => {
    const url = resource instanceof Request ? resource.url : String(resource);
    const method = init.method || (resource instanceof Request ? resource.method : 'GET');
    if (emmsIsUnsafeSameOrigin(method, url)) {
        const headers = new Headers(init.headers || (resource instanceof Request ? resource.headers : undefined));
        if (!headers.has('RequestVerificationToken')) headers.set('RequestVerificationToken', emmsCsrfToken());
        init = { ...init, headers };
    }
    return emmsNativeFetch(resource, init);
};

// Toasts: the one way the app tells the user something, instead of the browser's alert().
// type: 'success' | 'info' | 'warning' | 'danger'. Success and info close by themselves (not while hovered);
// warnings and errors stay until closed, so they can't disappear before they are read.
// Server messages arrive the same way: _Notification.cshtml turns TempData into the #emms-server-toasts list.
function emmsToast(message, type = 'info') {
    let stack = document.getElementById('emms-toasts');
    if (!stack) {
        stack = document.createElement('div');
        stack.id = 'emms-toasts';
        stack.className = 'emms-toasts';
        document.body.appendChild(stack);
    }

    const icons = { success: 'fa-circle-check', info: 'fa-circle-info', warning: 'fa-triangle-exclamation', danger: 'fa-circle-exclamation' };
    const urgent = type === 'warning' || type === 'danger';
    const toast = document.createElement('div');
    toast.className = `emms-toast emms-toast-${icons[type] ? type : 'info'}`;
    toast.setAttribute('role', urgent ? 'alert' : 'status');
    toast.innerHTML = `<i class="fa-solid ${icons[type] || icons.info}" aria-hidden="true"></i>
        <div class="emms-toast-text"></div>
        <button type="button" class="btn-close" aria-label="Close"></button>`;
    toast.querySelector('.emms-toast-text').textContent = message;

    const close = () => {
        toast.classList.add('emms-toast-out');
        setTimeout(() => toast.remove(), 200);
    };
    toast.querySelector('.btn-close').addEventListener('click', close);
    stack.appendChild(toast);

    if (!urgent) {
        let timer = setTimeout(close, 5000);
        toast.addEventListener('mouseenter', () => clearTimeout(timer));
        toast.addEventListener('mouseleave', () => { timer = setTimeout(close, 2500); });
    }
}

// For a script that reloads or navigates right after: the toast is shown on the next page instead.
function emmsToastAfterReload(message, type = 'info') {
    try {
        const queued = JSON.parse(sessionStorage.getItem('emms-toasts') || '[]');
        queued.push({ message, type });
        sessionStorage.setItem('emms-toasts', JSON.stringify(queued));
    } catch (e) {
        emmsToast(message, type);
    }
}

document.addEventListener('DOMContentLoaded', () => {
    let queued = [];
    try {
        queued = JSON.parse(sessionStorage.getItem('emms-toasts') || '[]');
        sessionStorage.removeItem('emms-toasts');
    } catch (e) { /* storage unavailable */ }

    const server = document.getElementById('emms-server-toasts');
    if (server) queued = queued.concat(JSON.parse(server.textContent));
    queued.forEach(t => emmsToast(t.message, t.type));
});

// In-app confirmation dialog, used instead of the browser's confirm(). Resolves to true when the user confirms.
// options: { okText: label of the confirm button, danger: true for a red confirm button (deleting, removing) }
function emmsConfirm(message, { okText = 'Confirm', danger = false } = {}) {
    return new Promise(resolve => {
        const element = document.createElement('div');
        element.className = 'modal fade emms-confirm';
        element.tabIndex = -1;
        element.innerHTML = `
            <div class="modal-dialog modal-dialog-centered">
                <div class="modal-content">
                    <div class="modal-body"><p class="mb-0"></p></div>
                    <div class="modal-footer">
                        <button type="button" class="btn btn-outline-secondary" data-bs-dismiss="modal">Cancel</button>
                        <button type="button" class="btn ${danger ? 'btn-danger' : 'btn-primary'}" data-role="ok"></button>
                    </div>
                </div>
            </div>`;
        element.querySelector('p').textContent = message;
        const ok = element.querySelector('[data-role="ok"]');
        ok.textContent = okText;
        document.body.appendChild(element);

        const dialog = new bootstrap.Modal(element);
        let confirmed = false;
        ok.addEventListener('click', () => { confirmed = true; dialog.hide(); });
        element.addEventListener('shown.bs.modal', () => ok.focus());
        element.addEventListener('hidden.bs.modal', () => {
            dialog.dispose();
            element.remove();
            resolve(confirmed);
        });
        dialog.show();
    });
}

// A form, or the button that submits it, with data-confirm="Question?" asks in the in-app dialog first.
// data-confirm-ok sets the confirm button's label; data-confirm-danger makes it red.
// Capture phase, so neither the page's submit handlers nor the loading overlay see the held-back submit.
document.addEventListener('submit', event => {
    const form = event.target;
    const source = [event.submitter, form].find(el => el && el.dataset.confirm);
    if (!source) return;
    if (form.dataset.confirmed) {
        delete form.dataset.confirmed;
        return;
    }

    event.preventDefault();
    event.stopImmediatePropagation();
    const submitter = event.submitter;
    emmsConfirm(source.dataset.confirm, {
        okText: source.dataset.confirmOk || 'Confirm',
        danger: 'confirmDanger' in source.dataset
    }).then(confirmed => {
        if (!confirmed) return;
        form.dataset.confirmed = '1';
        if (!form.requestSubmit) {
            emmsAddCsrfInput(form);
            form.submit();
        } else if (submitter) {
            form.requestSubmit(submitter);
        } else {
            form.requestSubmit();
        }
    });
}, true);



function printAssetDetail() {
    const asset = assetDetail || {};
    const movement = assetMovement || {};

    const getValue = val => val ?? '-';
    const formatDate = str => str ? new Date(str).toLocaleDateString() : '-';

    const getStatusBadge = (status, type) => {
        const classes = {
            ProcurementStatus: {
                0: 'status-new', // New
                1: 'status-info',
                2: 'status-warning',
                3: 'status-danger'
            },
            FunctionalStatus: {
                1: 'status-new', // Functional
                2: 'status-danger',
                3: 'status-warning'
            }
        };
        const className = classes[type]?.[status] || '';
        const label = type === 'ProcurementStatus'
            ? ['New', 'Used', 'Refurbished', 'Decommissioned'][status] || 'Unknown'
            : ['Unknown', 'Functional', 'NonFunctional', 'UnderMaintenance'][status] || 'Unknown';
        return `<div class="status-btn ${className}">${label}</div>`;
    };

    const location = getValue(movement.facility?.facilityName || movement.servicePoint?.name);
    const functionalStatus = movement.reason === 5
        ? `<div class="status-btn status-danger">Decommissioned</div>`
        : (movement.functionalStatus !== undefined
            ? getStatusBadge(movement.functionalStatus, 'FunctionalStatus')
            : '-');

    const placement = asset.isPlacement
        ? `${formatDate(asset.placementStartDate)} to ${formatDate(asset.placementEndDate)}`
        : 'No';

    const serviceable = asset.isServiceable
        ? `Every ${asset.serviceInterval ?? '-'} Month(s)`
        : 'No';

    const nextServiceDate = asset.isServiceable && asset.nextServiceDate
        ? formatDate(asset.nextServiceDate)
        : null;

    const logoPath = '/images/moh-letterhead.png';

    const html = `
        <html>
        <head>
            <title>Asset Detail</title>
            <link rel="stylesheet" href="/lib/bootstrap/dist/css/bootstrap.min.css">
            <style>
                body { padding: 20px; font-family: sans-serif; }
                .letterhead { text-align: center; border-bottom: 1px solid #333; padding-bottom: 8px; margin-bottom: 16px; }
                .letterhead img { height: 72px; width: auto; max-width: 100%; }
                .status-btn {
                    padding: 2px 6px;
                    border-radius: 4px;
                    display: inline-block;
                    color: #fff;
                    font-size: 0.85rem;
                }
                .status-new { background-color: #198754; }
                .status-info { background-color: #0dcaf0; }
                .status-warning { background-color: #ffc107; }
                .status-danger { background-color: #dc3545; }
            </style>
        </head>
        <body>
            <div class="letterhead">
                <img src="${logoPath}" alt="Kingdom of Eswatini, Ministry of Health"/>
            </div>
            <h4 class="text-center">EMMS Asset Detail Summary</h4>
            <div class="row">
                <div class="col-12 col-lg-4">
                    <dl class="row small">
                        <dt class="col-6 border p-1 m-0">Asset Tag</dt>
                        <dd class="col-6 border p-1 m-0">${getValue(asset.assetTagNumber)}</dd>

                        <dt class="col-6 border p-1 m-0">Category</dt>
                        <dd class="col-6 border p-1 m-0">${getValue(asset.category?.name)}</dd>

                        <dt class="col-6 border p-1 m-0">Sub Category</dt>
                        <dd class="col-6 border p-1 m-0">${getValue(asset.subCategory?.name)}</dd>

                        <dt class="col-6 border p-1 m-0">Item Name</dt>
                        <dd class="col-6 border p-1 m-0">${getValue(asset.itemName)}</dd>

                        <dt class="col-6 border p-1 m-0">Created By</dt>
                        <dd class="col-6 border p-1 m-0">${getValue(asset.user?.firstName)}</dd>

                        <dt class="col-6 border p-1 m-0">Procurement Date</dt>
                        <dd class="col-6 border p-1 m-0">${formatDate(asset.procurementDate)}</dd>

                        <dt class="col-6 border p-1 m-0">Procurement Status</dt>
                        <dd class="col-6 border p-1 m-0">${getStatusBadge(asset.statusId, 'ProcurementStatus')}</dd>
                    </dl>
                </div>

                <div class="col-12 col-lg-4">
                    <dl class="row small">
                        <dt class="col-6 border p-1 m-0">Department</dt>
                        <dd class="col-6 border p-1 m-0">${getValue(asset.department?.name)}</dd>

                        <dt class="col-6 border p-1 m-0">Manufacturer</dt>
                        <dd class="col-6 border p-1 m-0">${getValue(asset.manufacturer?.name)}</dd>

                        <dt class="col-6 border p-1 m-0">Serial Number</dt>
                        <dd class="col-6 border p-1 m-0">${getValue(asset.serialNumber)}</dd>

                        <dt class="col-6 border p-1 m-0">Model</dt>
                        <dd class="col-6 border p-1 m-0">${getValue(asset.model)}</dd>

                        <dt class="col-6 border p-1 m-0">Lifespan</dt>
                        <dd class="col-6 border p-1 m-0">${getValue(asset.lifespanQuantity)} years</dd>

                        <dt class="col-6 border p-1 m-0">Cost</dt>
                        <dd class="col-6 border p-1 m-0">E${getValue(asset.cost)}</dd>
                    </dl>
                </div>

                <div class="col-12 col-lg-4">
                    <dl class="row small">
                        <dt class="col-6 border p-1 m-0">Placement</dt>
                        <dd class="col-6 border p-1 m-0">${placement}</dd>

                        <dt class="col-6 border p-1 m-0">Donated</dt>
                        <dd class="col-6 border p-1 m-0">${asset.isDonated ? 'Yes' : 'No'}</dd>

                        <dt class="col-6 border p-1 m-0">Vendor</dt>
                        <dd class="col-6 border p-1 m-0">${getValue(asset.vendor?.name)}</dd>

                        <dt class="col-6 border p-1 m-0">Service Provider</dt>
                        <dd class="col-6 border p-1 m-0">${getValue(asset.serviceProvider?.name)}</dd>

                        <dt class="col-6 border p-1 m-0">Current Location</dt>
                        <dd class="col-6 border p-1 m-0">${location}</dd>

                        <dt class="col-6 border p-1 m-0">Functional Status</dt>
                        <dd class="col-6 border p-1 m-0">${functionalStatus}</dd>

                        <dt class="col-6 border p-1 m-0">Serviceable</dt>
                        <dd class="col-6 border p-1 m-0">${serviceable}</dd>

                        ${nextServiceDate ? `
                        <dt class="col-6 border p-1 m-0">Next Service Date</dt>
                        <dd class="col-6 border p-1 m-0">${nextServiceDate}</dd>` : ''}
                    </dl>
                </div>
            </div>
        </body>
        </html>
    `;

    const printWindow = window.open('', '_blank', 'width=900,height=800');
    printWindow.document.write(html);
    printWindow.document.close();
    printWindow.onafterprint = () => printWindow.close();

    // Print only once the letterhead has loaded, or it can be missing from the first printout
    const letterhead = printWindow.document.querySelector('.letterhead img');
    const print = () => printWindow.print();
    if (letterhead.complete) print();
    else letterhead.onload = letterhead.onerror = print;
}




// Posts a list of asset tags as a normal form submit (name="tags"), optionally into a new tab.
function emmsPostTags(action, tags, target) {
    const form = document.createElement('form');
    form.method = 'post';
    form.action = action;
    if (target) form.target = target;
    tags.forEach(tag => {
        const input = document.createElement('input');
        input.type = 'hidden';
        input.name = 'tags';
        input.value = tag;
        form.appendChild(input);
    });
    document.body.appendChild(form);
    emmsAddCsrfInput(form); // form.submit() skips the submit event
    form.submit();
    form.remove();
}

// Opens the server-rendered sticker sheet (Code 128 barcode of each asset tag) in a new tab.
// tags: optional array of asset tags; defaults to the ticked .asset-checkbox values.
function printSelectedStickers(tags) {
    const selected = (tags || Array.from(document.querySelectorAll('.asset-checkbox:checked')).map(cb => cb.value))
        .map(t => (t || '').trim())
        .filter(t => t !== '');

    if (selected.length === 0) {
        emmsToast('Select at least one asset to print stickers for.', 'warning');
        return;
    }

    emmsPostTags('/AssetManagement/PrintStickers', selected, '_blank');
}

// Short confirmation tone for scanning: high beep = accepted, low buzz = rejected.
function emmsBeep(ok) {
    try {
        const ctx = emmsBeep.ctx || (emmsBeep.ctx = new (window.AudioContext || window.webkitAudioContext)());
        const osc = ctx.createOscillator();
        const gain = ctx.createGain();
        osc.type = ok ? 'sine' : 'square';
        osc.frequency.value = ok ? 880 : 220;
        gain.gain.value = 0.08;
        osc.connect(gain);
        gain.connect(ctx.destination);
        osc.start();
        osc.stop(ctx.currentTime + (ok ? 0.12 : 0.35));
    } catch (e) { /* audio not available */ }
}


function emmsCameraScanSupported() {
    return 'BarcodeDetector' in window && !!(navigator.mediaDevices && navigator.mediaDevices.getUserMedia);
}

async function emmsOpenCameraScanner(onCode) {
    const detector = new BarcodeDetector({ formats: ['code_128'] });
    const overlay = document.createElement('div');
    overlay.style.cssText = 'position:fixed;inset:0;z-index:2000;background:rgba(0,0,0,.85);display:flex;flex-direction:column;align-items:center;justify-content:center;padding:16px;';
    overlay.innerHTML = '<video playsinline muted style="max-width:95vw;max-height:65vh;border-radius:8px;background:#000"></video>' +
        '<div class="text-white mt-2" data-role="status">Point the camera at the barcode</div>' +
        '<button type="button" class="btn btn-light mt-3">Done</button>';
    document.body.appendChild(overlay);

    const video = overlay.querySelector('video');
    const status = overlay.querySelector('[data-role="status"]');
    let stream = null, stopped = false, lastCode = null, lastAt = 0;
    const stop = () => { stopped = true; if (stream) stream.getTracks().forEach(t => t.stop()); overlay.remove(); };
    overlay.querySelector('button').addEventListener('click', stop);

    try {
        stream = await navigator.mediaDevices.getUserMedia({ video: { facingMode: 'environment' } });
        video.srcObject = stream;
        await video.play();
    } catch (e) {
        stop();
        emmsToast('Camera not available: ' + e.message, 'danger');
        return;
    }

    const tick = async () => {
        if (stopped) return;
        try {
            const codes = await detector.detect(video);
            const now = Date.now();
            if (codes.length && (codes[0].rawValue !== lastCode || now - lastAt > 2000)) {
                lastCode = codes[0].rawValue;
                lastAt = now;
                status.textContent = 'Read ' + lastCode;
                onCode(lastCode);
            }
        } catch (e) { /* frame not ready */ }
        requestAnimationFrame(tick);
    };
    tick();
}

// Barcode scanner support. Handheld scanners act as a keyboard: they type the code, then press Enter.
// - A button with data-scan-target="<input id>" focuses and selects that input, so the next scan replaces it.
// - On input[data-scan-input], Enter trims the value and moves to the next field instead of submitting the form.
//   (Textareas are left alone so each scan lands on a new line.)
document.addEventListener('click', function (e) {
    const btn = e.target.closest ? e.target.closest('[data-scan-target]') : null;
    if (!btn) return;
    const target = document.getElementById(btn.dataset.scanTarget);
    if (!target || target.disabled) return;
    target.focus();
    if (typeof target.select === 'function') target.select();
});

document.addEventListener('keydown', function (e) {
    const el = e.target;
    if (e.key !== 'Enter' || !el.matches || !el.matches('input[data-scan-input]')) return;
    e.preventDefault();
    el.value = el.value.trim();
    el.dispatchEvent(new Event('change', { bubbles: true }));

    const scope = el.form || el.closest('.modal') || document;
    const focusable = Array.from(scope.querySelectorAll('input:not([type=hidden]):not([disabled]), select:not([disabled]), textarea:not([disabled])'))
        .filter(x => x.offsetParent !== null);
    const next = focusable[focusable.indexOf(el) + 1];
    if (next) next.focus();
});

function exportToExcel() {
    var table = $('#assetTable').DataTable();
    var allRows = table.rows().nodes(); // Get all HTML rows
    var headers = [];


    // Get headers from thead, skipping first column and Procurement Status column
    $('#assetTable thead th').each(function (index) {
        // Skip first column (index 0) and column with text "Procurement Status"
        var headerText = $(this).text().trim();
        if (index !== 0 && headerText !== "Procurement Status") {
            headers.push(headerText);
        }
    });

    var data = [];
    data.push(headers); // Add headers

    // Loop through each row
    $(allRows).each(function () {
        var rowData = [];

        $(this).find('td').each(function (colIndex) {
            var headerText = $('#assetTable thead th').eq(colIndex).text().trim();

            // Skip first column and "Procurement Status"
            if (colIndex !== 0 && headerText !== "Procurement Status") {
                rowData.push($(this).text().trim());
            }

            // Handle Procurement Status specially: extract plain text from div
            if (headerText === "Procurement Status") {
                var statusText = $(this).text().trim();
                rowData.push(statusText);
            }
        });

        data.push(rowData);
    });

    // Create worksheet and workbook
    var worksheet = XLSX.utils.aoa_to_sheet(data);
    var workbook = XLSX.utils.book_new();
    XLSX.utils.book_append_sheet(workbook, worksheet, "Assets");

    // Download as Excel file
    XLSX.writeFile(workbook, 'EMMSAssets.xlsx');
}


       
      




// Service request links are rendered in several asset tables (some built in JS),
// so one delegated handler posts them through the hidden form in _Layout.
document.addEventListener('click', function (event) {
    const link = event.target.closest('.js-service-request');
    if (!link) return;
    event.preventDefault();
    const form = document.getElementById('serviceRequestForm');
    if (!form) return;
    form.querySelector('input[name="id"]').value = link.dataset.assetId;
    emmsAddCsrfInput(form);
    form.submit();
});

// Every DataTable folds the columns that don't fit into an expandable row (Responsive) instead of scrolling sideways.
// Responsive drops columns right to left. On a <th>, data-priority="n" keeps a column longer (lower n = kept longer),
// class="all" always keeps it and class="none" always folds it. By default the first column and an "Action(s)"
// column are kept, so every row stays identifiable and actionable. Priorities are read when a table is set up,
// so they are stamped here, before the pages' ready handlers create their tables.
if (window.jQuery && $.fn.dataTable) {
    $.extend($.fn.dataTable.defaults, { responsive: true, autoWidth: false });

    document.querySelectorAll('table > thead > tr:last-child').forEach(row => {
        const cells = Array.from(row.children);
        const keep = (th, priority) => { if (th && !th.hasAttribute('data-priority')) th.setAttribute('data-priority', priority); };
        keep(cells[0], 1);
        keep(cells.find(th => /^actions?$/i.test(th.textContent.trim())), 2);
    });

    // Tables in hidden tabs, modals and collapses are measured at zero width; fit them again once shown
    $(document).on('shown.bs.tab shown.bs.modal shown.bs.collapse', () => {
        $.fn.dataTable.tables({ visible: true, api: true }).columns.adjust().responsive.recalc();
    });

    // Responsive opens a row's details on a click anywhere in its first cell; a checkbox there (row selection)
    // should only tick. Capture phase, so Responsive's handler on the table body never sees the click.
    ['click', 'mousedown', 'mouseup'].forEach(type => document.addEventListener(type, e => {
        if (e.target.matches('td.dtr-control input[type=checkbox]')) e.stopPropagation();
    }, true));
}

// Searchable column filters for a DataTable, in a bar above the table rather than in its header, so they don't
// widen the table and stay reachable when Responsive folds their column away. Below 1200px the bar folds behind
// a "Filters" button that shows how many are applied.
// options: { columnName: [{ text, value }] } (built on the server from lookup lists / enums), keyed by the
// DataTables column `name`. Server-side tables send the selected value as that column's search and the server
// matches it exactly. Client-side tables match it exactly against the cell's search text (its data-search
// attribute when set, else its text), so for those the option values are the cell values.
// clearButton: optional selector for a button that resets every filter; it is shown only while one is applied.
// A query-string value named like a column (e.g. ?Status=2&Facility=5, from the dashboard charts) presets that filter.
function emmsColumnFilters(api, options, clearButton) {
    let clearing = false;   // resetting the pickers fires their change events; draw once at the end instead
    let preset = false;
    const params = new URLSearchParams(window.location.search);
    const serverSide = api.settings()[0].oFeatures.bServerSide;
    const escapeRegex = text => text.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
    const applySearch = (column, value) => serverSide
        ? column.search(value, false, false)
        : column.search(value ? `^\\s*${escapeRegex(value)}\\s*$` : '', true, false);

    const fieldsId = `${api.table().node().id}-filters`;
    const bar = $(`
        <div class="emms-filter-bar">
            <button type="button" class="btn btn-sm btn-outline-secondary d-xl-none" data-bs-toggle="collapse"
                    data-bs-target="#${fieldsId}" aria-expanded="false" aria-controls="${fieldsId}">
                <i class="fa fa-filter me-1"></i> Filters <span class="badge bg-primary ms-1 d-none" data-role="count"></span>
            </button>
            <div class="collapse d-xl-block" id="${fieldsId}"><div class="emms-filter-grid"></div></div>
        </div>`);
    const grid = bar.find('.emms-filter-grid');

    // Above the table's scroll wrapper, so the bar never scrolls sideways with the table
    const container = $(api.table().container());
    const wrapper = container.parents('.table-responsive, .table-wrapper').last();
    bar.insertBefore(wrapper.length ? wrapper : container);

    const selects = [];
    api.columns().every(function () {
        const column = this;
        const name = api.settings()[0].aoColumns[column.index()].sName;
        const items = options[name];
        if (!items) return;

        const select = document.createElement('select');
        select.className = 'column-filter';
        select.setAttribute('aria-label', `Filter by ${column.title()}`);
        select.add(new Option('All', ''));
        items.forEach(o => select.add(new Option(o.text, o.value)));
        selects.push(select);

        const wanted = params.get(name);
        if (wanted && items.some(o => o.value === wanted)) {
            select.value = wanted;
            applySearch(column, wanted);
            preset = true;
        }

        $('<div class="emms-filter-field"></div>')
            .append($('<label></label>').text(column.title()))
            .append(select)
            .appendTo(grid);

        $(select)
            .selectpicker({
                liveSearch: true,
                liveSearchPlaceholder: 'Search…',
                size: 8,
                width: '100%',
                container: 'body',
                style: 'btn-light btn-sm'
            })
            .on('changed.bs.select', () => {
                applySearch(column, select.value);
                if (!clearing) api.draw();
            });
    });

    if (selects.length === 0) {
        bar.remove();
        return;
    }

    const syncState = () => {
        const applied = selects.filter(s => s.value !== '').length;
        bar.find('[data-role="count"]').text(applied).toggleClass('d-none', applied === 0);
        if (clearButton) $(clearButton).toggleClass('d-none', applied === 0);
    };
    api.on('draw', syncState);
    syncState();

    if (clearButton) {
        $(clearButton).on('click', function () {
            clearing = true;
            $(selects).selectpicker('val', '');
            clearing = false;
            api.columns().search('').draw();
        });
    }

    // A filter preset from the link is shown open, so it is clear why the list is narrowed
    if (preset) {
        $(`#${fieldsId}`).addClass('show');
        api.draw();
    }
}

// Opens a list page on one row: ?focus=<id> scrolls to and highlights the element with data-focus-id=<id>,
// showing its tab and DataTables page first; &act=<name> then clicks that row's [data-focus-act=<name>] button
// (e.g. the Close dialog). Used by the dashboard's row buttons. Runs on window load, after the pages'
// own DataTables set-up in their ready handlers.
function emmsFocusRow() {
    const params = new URLSearchParams(window.location.search);
    const id = params.get('focus');
    if (!id) return;
    const act = params.get('act');
    const actSelector = act ? `[data-focus-act="${CSS.escape(act)}"]` : null;

    // The same id can be listed twice on a page (e.g. a request in two tables); prefer the row that has the action
    const rows = Array.from(document.querySelectorAll('[data-focus-id]')).filter(el => el.dataset.focusId === id);
    const row = (actSelector && rows.find(r => r.querySelector(actSelector))) || rows[0];
    if (!row) return;

    const pane = row.closest('.tab-pane');
    if (pane && !pane.classList.contains('active') && window.bootstrap) {
        const tab = document.querySelector(`[data-bs-target="#${pane.id}"]`);
        if (tab) bootstrap.Tab.getOrCreateInstance(tab).show();
    }

    const table = row.closest('table');
    if (table && window.jQuery && $.fn.dataTable && $.fn.dataTable.isDataTable(table)) {
        const dt = $(table).DataTable();
        const index = dt.rows({ order: 'applied', search: 'applied' }).nodes().toArray().indexOf(row);
        if (index >= 0) dt.page(Math.floor(index / dt.page.len())).draw('page');
    }

    row.classList.add('emms-focus');
    row.scrollIntoView({ block: 'center' });

    const button = actSelector ? row.querySelector(actSelector) : null;
    if (button) button.click();
}

window.addEventListener('load', emmsFocusRow);
