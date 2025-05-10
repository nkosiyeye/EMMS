// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.



function printAssetDetail() {
    const printWindow = window.open('', '_blank', 'width=800,height=600');
    printWindow.document.write(`
                <html>
                    <head>
                        <title>Print Asset ${assetDetail.assetId}</title>
                        <link rel="stylesheet" href="/lib/bootstrap/dist/css/bootstrap.min.css">
                    </head>
                    <body>

                        <div class="card-body">
                            <div class="row">
                                <div class="col-md-4">
                                    <dl class="row">
                                        <dt class="col-sm-6 border p-1 m-0">Asset ID</dt>
                                        <dd class="col-sm-6 border p-1 m-0">${assetDetail.assetId}</dd>

                                        <dt class="col-sm-6 border p-1 m-0">Category</dt>
                                        <dd class="col-sm-6 border p-1 m-0">${assetDetail.category}</dd>

                                        <dt class="col-sm-6 border p-1 m-0">Sub Category</dt>
                                        <dd class="col-sm-6 border p-1 m-0">${assetDetail.subCategory}</dd>

                                        <dt class="col-sm-6 border p-1 m-0">Item Name</dt>
                                        <dd class="col-sm-6 border p-1 m-0">${assetDetail.itemName}</dd>
                                    </dl>
                                </div>
                                <div class="col-md-4">
                                    <dl class="row">

                                        <dt class="col-sm-6 border p-1 m-0">Department</dt>
                                        <dd class="col-sm-6 border p-1 m-0">${assetDetail.department}</dd>

                                        <dt class="col-sm-6 border p-1 m-0">Manufacturer</dt>
                                        <dd class="col-sm-6 border p-1 m-0">${assetDetail.manufacturer}</dd>

                                        <dt class="col-sm-6 border p-1 m-0">Serial Number</dt>
                                        <dd class="col-sm-6 border p-1 m-0">${assetDetail.serialNumber}</dd>


                                        <dt class="col-sm-6 border p-1 m-0">Model</dt>
                                        <dd class="col-sm-6 border p-1 m-0">${assetDetail.model}</dd>

                                    </dl>
                                </div>
                                <div class="col-md-4">
                                    <dl class="row">

                                        <dt class="col-sm-6 border p-1 m-0">Placement</dt>
                                        <dd class="col-sm-6 border p-1 m-0">
                                         ${
                                            assetDetail.isPlacement
                                                ? `Yes ${new Date(assetDetail.placementStartDate).toLocaleDateString()} to ${new Date(assetDetail.placementEndDate).toLocaleDateString()}`
                                                : "No"
                                         }
                                        </dd>

                                        <dt class="col-sm-6 border p-1 m-0">Donated</dt>
                                        <dd class="col-sm-6 border p-1 m-0">Yes</dd>

                                        <dt class="col-sm-6 border p-1 m-0">Vendor</dt>
                                        <dd class="col-sm-6 border p-1 m-0">${assetDetail.vendor}</dd>

                                        <dt class="col-sm-6 border p-1 m-0">Service Provider</dt>
                                        <dd class="col-sm-6 border p-1 m-0">${assetDetail.serviceProvider}</dd>
                                    </dl>
                                </div>
                            </div>
                            <div class="row">
                                <div class="col-md-4">
                                    <dl class="row">
                                        <dt class="col-sm-6 border p-1 m-0">Quanity</dt>
                                        <dd class="col-sm-6 border p-1 m-0">${assetDetail.quantity}</dd>

                                        <dt class="col-sm-6 border p-1 m-0">Procument Date</dt>
                                        <dd class="col-sm-6 border p-1 m-0">${new Date(assetDetail.procurementDate).toLocaleDateString()}</dd>

                                        <dt class="col-sm-6 border p-1 m-0">Status</dt>
                                        <dd class="col-sm-6 border p-1 m-0">${assetDetail.status}</dd>

                                    </dl>
                                </div>
                                <div class="col-md-4">
                                    <dl class="row">

                                        <dt class="col-sm-6 border p-1 m-0">Lifespan</dt>
                                        <dd class="col-sm-6 border p-1 m-0">${assetDetail.lifespanQuantity} ${assetDetail.lifespanPeriod}</dd>

                                        <dt class="col-sm-6 border p-1 m-0">Cost</dt>
                                        <dd class="col-sm-6 border p-1 m-0">${assetDetail.cost}</dd>

                                        <dt class="col-sm-6 border p-1 m-0">Serial Number</dt>
                                        <dd class="col-sm-6 border p-1 m-0">${assetDetail.serialNumber}</dd>

                                    </dl>
                                </div>
                                <div class="col-md-4">
                                    <dl class="row">

                                        <dt class="col-sm-6 border p-1 m-0">Current Location</dt>
                                        <dd class="col-sm-6 border p-1 m-0">Lobamba Clinic</dd>

                                        <dt class="col-sm-6 border p-1 m-0">Functional Status</dt>
                                        <dd class="col-sm-6 border p-1 m-0">Working</dd>

                                        <dt class="col-sm-6 border p-1 m-0">Condition</dt>
                                        <dd class="col-sm-6 border p-1 m-0">Good</dd>
                                    </dl>
                                </div>
                            </div>
                        </div>
                    </body>
                </html>
            `);

    printWindow.document.close();
    printWindow.print();
    printWindow.onafterprint = () => printWindow.close();
    console.log(assetDetail);
}
function printSelectedStickers() {
    const selectedAssets = Array.from(document.querySelectorAll('.asset-checkbox:checked'))
        .map(checkbox => JSON.parse(checkbox.value));

    if (selectedAssets.length === 0) {
        alert("No assets selected for printing.");
        return;
    }

    const printWindow = window.open('', '_blank', 'width=800,height=600');
    printWindow.document.write(`
                <html>
                    <head>
                        <title>Print Asset Stickers</title>
                        <link rel="stylesheet" href="/lib/bootstrap/dist/css/bootstrap.min.css">
                        <style>
                           .sticker {
                                    width: 3.5in; /* Standard sticker width */
                                    height: 2in; /* Standard sticker height */
                                    border: 1px solid black;
                                    padding: 15px;
                                    font-family: Arial, sans-serif;
                                    font-size: 12px;
                                    display: flex;
                                    flex-direction: column;
                                    justify-content: space-between;
                                }

                                .sticker-header {
                                    text-align: center;
                                    font-weight: bold;
                                    font-size: 14px;
                                }

                                .sticker-details {
                                    margin-top: 5px;
                                }

                                    .sticker-details p {
                                        margin: 2px 0;
                                    }

                                .qr-code {
                                    display: flex;
                                    justify-content: center;
                                    align-items: center;
                                    margin-bottom: 15px;
                                }

                                    .qr-code img {
                                        width: 50px;
                                        height: 50px;
                                    }
                        </style>
                    </head>
                    <body>
                        ${selectedAssets.map(asset => `
                        <div class="sticker">
                            <div class="sticker-header">
                                EMMS
                            </div>
                            <div class="row py-0">
                                <div class="sticker-details col-md-6">
                                        <p><strong>Asset ID:</strong> ${asset.id}</p>
                                        <p><strong>Serial Number:</strong> ${asset.serialNumber}</p>
                                        <p><strong>Model:</strong> ${asset.model}</p>
                                </div>
                                <div class="qr-code col-md-6">
                                    <img src="https://api.qrserver.com/v1/create-qr-code/?size=100x100&data=AS-001" alt="QR Code">
                                </div>
                            </div>
                        </div>
                        `).join('')}
                    </body>
                </html>
            `);

    printWindow.document.close();
    //printWindow.print();
   // printWindow.onafterprint = () => printWindow.close();
}


       
      



