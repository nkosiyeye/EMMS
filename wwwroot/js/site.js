// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.



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

    const logoPath = '/images/Coat_of_arms_of_Eswatini.svg';

    const html = `
        <html>
        <head>
            <title>Asset Detail</title>
            <link rel="stylesheet" href="/lib/bootstrap/dist/css/bootstrap.min.css">
            <style>
                body { padding: 20px; font-family: sans-serif; }
                .logo { max-height: 60px; margin-bottom: 20px; }
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
            <div class="text-center">
                <img src="${logoPath}" alt="Logo" class="logo"/>
                <h4>EMMS Asset Detail Summary</h4>
            </div>
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
    printWindow.print();
    printWindow.onafterprint = () => printWindow.close();
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
        alert("No assets selected for printing.");
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

// Phone/tablet camera scanning (Code 128) where the browser supports BarcodeDetector.
// Needs HTTPS (or localhost). Calls onCode for every new code until the overlay is closed.
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
        alert('Camera not available: ' + e.message);
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

function fnExcelReport() {
    var tab_text = "<table border='2px'><tr bgcolor='#87AFC6'>";
    var tab = document.getElementById('assetTable'); // id of table

    // Loop through each row of the table
    for (var j = 0; j < tab.rows.length; j++) {
        tab_text += "<tr>";

        // Loop through each cell in the row, excluding the last cell
        for (var k = 0; k < tab.rows[j].cells.length - 1; k++) {
            tab_text += tab.rows[j].cells[k].outerHTML;
        }

        tab_text += "</tr>";
    }

    tab_text += "</table>";

    // Remove unwanted elements like links, images, and input fields
    tab_text = tab_text.replace(/<A[^>]*>|<\/A>/g, ""); // Remove links
    tab_text = tab_text.replace(/<img[^>]*>/gi, ""); // Remove images
    tab_text = tab_text.replace(/<input[^>]*>|<\/input>/gi, ""); // Remove input fields

    var msie = window.navigator.userAgent.indexOf("MSIE ");

    // If Internet Explorer
    if (msie > 0 || !!navigator.userAgent.match(/Trident.*rv\:11\./)) {
        txtArea1.document.open("txt/html", "replace");
        txtArea1.document.write(tab_text);
        txtArea1.document.close();
        txtArea1.focus();

        sa = txtArea1.document.execCommand("SaveAs", true, "ExportedData.xls");
    } else {
        // For other browsers
        sa = window.open('data:application/vnd.ms-excel,' + encodeURIComponent(tab_text));
    }

    return sa;
}
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


       
      



