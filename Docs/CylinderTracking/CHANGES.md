# EMMS – Cylinder Tracking and Scanning: Changes

This file covers what changed in EMMS for oxygen cylinder tracking, barcode scanning and batch movements. It also says how to deploy and verify the changes.

The work extends the existing Equipment Registration and Asset Movement flows; it isn't a separate module. Most of it applies to **every** asset: stickers, tags, scanning, the movement queue and batch receipt. Cylinders are an asset **Category** with a flag that adds a few fields.

The work landed in four commits, one per phase. Each commit builds and passes its tests on its own.

| Commit | Phase | Summary |
|---|---|---|
| `eb97e94` | 0 – Safety net | Working barcode stickers, sequence-based asset tags, serial validation |
| `ba77572` | 1 – Cylinder registration | Category flag `CylinderProfile`, Size/Content, initial location, serial list |
| `d8be5d8` | 2 – Scan-to-move | Movement Queue, batch movements, batch approval and gate pass, patient capture |
| `54f8bc2` | 3 – Request to dispatch | Cylinder requests and orders, pre-approved dispatch, scan-to-receive |

---

## Deploying

### 1. Apply the migrations, in order

```
dotnet ef database update
```

| Migration | Phase | What it does |
|---|---|---|
| `AssetTagSequence` | 0 | `AssetTagNumber` becomes `nvarchar(50)` with a unique index; creates sequence `AssetTagSeq`, starting above the highest existing tag number |
| `CylinderRegistration` | 1 | Creates table `AssetCylinderDetails` and column `Facilities.IsCentralStore`; seeds lookups (see below) |
| `SeedCentralMedicalStores` | 1 | Flags the Central Medical Stores facility as the central store, or creates it if none exists |
| `DeactivateLegacyOxygenCylinder` | 1 | Deactivates the old "Oxygen Cylinder" subcategory under Medical Equipment, if no asset uses it |
| `MovementQueue` | 2 | Creates tables `MovementQueueItems` and `HomeBasedCarePatients`; adds `AssetMovement.BatchId` and `AssetMovement.PatientId` |
| `CylinderOrders` | 3 | Creates tables `CylinderRequests`, `CylinderOrders`, `CylinderOrderItems` and sequence `CylinderOrderSeq` |

**If `AssetTagSequence` stops with error 50001**, the database already has duplicate asset tags. A unique index can't be created over them, and the migration won't silently re-tag assets that may already carry a printed label.

To fix it:
1. Run step 1 of `Docs/CylinderTracking/fix-duplicate-tags.sql`. It's read-only and lists the duplicates.
2. Reprint labels for the assets it will re-tag. It keeps the oldest asset on each tag.
3. Uncomment step 2, run it, then re-run the migration.

### 2. Create the new roles

1. Go to **Admin → User Roles → Create**. The User Type list now includes **Pharmacist (6)** and **CMS Officer (7)**.
2. Create one role for each, with the permission settings you want.
3. Assign users to the roles. A CMS Officer only gets CMS rights when their user belongs to the facility flagged `IsCentralStore`.

### 3. Ask users to log in again

The session stores the user's facility, and that facility now carries the new `IsCentralStore` flag. CMS rights are always re-checked against the database, but other screens read the session.

### Seeded lookups

The seeds are idempotent: rows that already exist are reused, and only missing flags are filled in. Admins maintain the values under **Admin → Lookups**.

| List | Item | Flags (`FlagsJson`) |
|---|---|---|
| Category | Cylinder | `{"CylinderProfile":true}` |
| SubCategory | Oxygen Cylinder (under Cylinder) | `{"RequiresSerial":true}` |
| Service Point | Home Based Care | `{"RequiresPatient":true}` |
| Cylinder Size | 2 L, 5 L, 10 L, 47 L | – |
| Cylinder Content | Oxygen, Medical Air | – |

All flags are booleans. Every flag name is defined in `Constants/Constant.cs` (`LookupFlags`, `LookupListNames`). The lookup admin screens show the checkboxes for each list:

| List | Checkbox |
|---|---|
| SubCategory | Requires Serial |
| Category | Cylinder Profile |
| Service Point | Requires Patient |

---

## Phase 0 – Safety net

**Stickers (finding B1).** Print Stickers used to crash, because it called `JSON.parse` on a plain tag. It also printed a fixed "AS-001" QR code from an external website.
- It now opens a server-rendered sticker sheet: `AssetManagement/PrintStickers`, view `stickers.cshtml`.
- Each sticker carries a **Code 128** barcode of the asset tag, with the serial, item and size underneath. The layout is 2.63 × 1 in, three across.
- The barcode comes from a small encoder with no dependencies, `Utility/Code128.cs`. It works offline, and its output was cross-checked against JsBarcode.

**Multi-select on Manage Assets.**
- Ticked assets stay ticked across pages.
- There's a select-all-on-this-page box and a "Clear selection" link.
- **Print Stickers for Selected (n)** prints everything ticked. **Move Selected (n)** arrives in Phase 2.

**Asset tags (finding D1).**
- Tags come from the `AssetTagSeq` SQL sequence, with one database round trip per batch (`Service/AssetTagService.cs`, `Data/Repository/AssetTagSequenceRepo.cs`).
- Previously they were "count all assets + 1", which produced duplicates.
- The tag format is unchanged: `{FacilityCode}AS-{n:D3}`, for example `S003AS-120`.

**Serial numbers.**
- Serials are cleaned up the same way on the server and in the browser (`Utility/SerialNumber.cs`): spaces trimmed, and the CR/LF/TAB characters a scanner adds removed.
- `SaveAssets` now checks the sub-category's `RequiresSerial` flag. It uses the same default as the Quick Registration form: required unless the flag says `false`.
- It rejects serials that appear twice in the list, and serials already registered (checked in a single query). Every problem is listed, by row.
- Rows copied for a quantity above 1 now keep their warranty dates and the Is Idle setting (finding D4).
- The serial index stays **non-unique**, with uniqueness checked in the app. That way a hidden asset can be re-registered, and two manufacturers can use the same serial.

## Phase 1 – Category-driven registration

When the chosen Category has `CylinderProfile`, the Register, Quick Registration and Edit forms **add** Size and Content. A required initial location is also enforced.
- The initial location reuses the existing "Already Deployed" section. It's ticked and required, and the facility defaults to the central store.
- No existing field is hidden, and no existing `[Required]` attribute changed.
- The server reads the category's flag itself rather than trusting the form, and validates the cylinder fields in `Service/CylinderRules.cs`.
- The asset, its `AssetCylinderDetail` row and the initial movement are saved in one `SaveChanges`.

Other additions:
- **Scan button** next to every Serial Number field. It focuses the field for a handheld (keyboard-wedge) scanner. Enter from the scanner moves to the next field instead of submitting the form.
- **Serial list** in Quick Registration: type, paste or scan many serials, one per line.
  - Live counts show duplicates in the list, rows already in the table, and serials already registered (via `CheckSerials`).
  - You fill in the shared details once in the Add Asset popup, and one row is staged per serial.
- **Detail page and sticker** show the Size.
- **Fixed an existing bug:** editing a lookup item in any list other than SubCategory used to wipe its `FlagsJson`.

## Phase 2 – Scan-to-move (Movement Queue)

**Asset Movement → Movement Queue.** Assets are added by:
- scanning, or typing the tag into the same box;
- **Move Selected** on Manage Assets, for facilities without a scanner;
- the phone camera, where the browser supports `BarcodeDetector` (Chrome on Android; needs HTTPS).

There's a beep for accepted scans and a low buzz for rejected ones.

**Scan checks** (`Service/MovementQueueService.cs` → `Evaluate`). The first failure wins:

`NotFound` → `Inactive` → `NoLocation` → `InTransit` → `StolenMissing` → `Decommissioned` → `OpenWorkRequest` → `NotAtYourFacility` → `MixedSource` → `AlreadyQueued`

- `Decommissioned` and `OpenWorkRequest` keep rules the single Move Asset flow already applies.
- Administrators skip `NotAtYourFacility`, but not `MixedSource`.
- "Current location" means the facility of the asset's latest *received* movement.

**Submit Batch.**
- It rechecks every queued asset first.
- In one explicit database transaction, it creates one `AssetMovement` per asset sharing a new `BatchId`, clears the queue, and sends **one notification per approver** for the whole batch.
- If anything fails, nothing moves and the queue stays as it was. The explicit transaction matters because `NotificationService` saves to the database on its own.

**Batch page.** Shows status counts, **Approve Batch** (Administrator or Facility Manager, one notification), and a **batch gate pass** listing every asset. Each row on Movement History links to its batch.

**Home Based Care.**
- Moving to a service point flagged `RequiresPatient` asks for the patient's number, name, surname and address, in both the queue and the single Move Asset form.
- Patients are matched by number, so a repeat patient reuses the same record.
- Only administrators and the two facilities involved can see patient details on the batch page.

## Phase 3 – Request, order, dispatch, receive

The **Cylinder Requests** page is a new sidebar item, hidden from data collectors.

| Step | Who | What happens |
|---|---|---|
| Request | Facility user | Chooses item, size, content and quantity, and gives the requisition name and number |
| Approve / reject | Facility **Pharmacist** | Approves, or rejects with a reason. A rejected request can be revised and resubmitted |
| Order | **CMS Officer** (user at the central store) | Creates an order (`ORD-yyyy-nnnn`). Can modify the quantity (the order then needs re-submitting) or mark it **plant down**, which closes the order and returns the request to the facility |
| Dispatch | CMS Officer | The Movement Queue opens in **dispatch mode** with a running counter (`12 / 20`) |
| Receive | Facility | **Scan to Receive** card on the Receive page |

**Dispatch mode.**
- Adds three checks: `NotAtCentralStore`, `WrongItem` (item, size or content differs from the request) and `OrderFull`.
- The destination and reason come from the order, not the form.
- Movements are **pre-approved**, each cylinder is recorded as a `CylinderOrderItem`, and the facility gets one "on its way" notification.

**Scan to Receive.**
- Pick an incoming batch; dispatches are labelled with their order number.
- Scan or tick each item. The camera works here too.
- Unscanned items stay in transit and get a follow-up note, and the sending facility is notified. They don't block the rest of the receipt, and can be received or rejected later through the existing screens.

**Order status** is derived from the order's items' movements (`CylinderOrderService.DeriveStatus`):
- **Dispatched** once anything has been sent; **Received** once everything has been received.
- If the facility rejects a cylinder, it stays at CMS and the order stays Dispatched. CMS can then send a replacement.
- The existing one-at-a-time receive and reject also update the order's status.

**New enum values**, appended so no existing numbers change:

| Enum | New values |
|---|---|
| `UserType` | `Pharmacist = 6`, `CmsOfficer = 7` |
| `MovementReason` | `Dispatch = 10`, `Return = 11` |

- Dispatch is created only from an order. It's hidden on the manual move forms and rejected by the server.
- Return is accepted only when the destination is the central store.

---

## Data model added

| Table | Purpose |
|---|---|
| `AssetCylinderDetails` | Size and Content for cylinder assets (1:1 with `Assets`) |
| `MovementQueueItems` | A user's queued assets (unique per asset) |
| `HomeBasedCarePatients` | Patients for `RequiresPatient` service points |
| `CylinderRequests`, `CylinderOrders`, `CylinderOrderItems` | Request → order → dispatched cylinder (`MovementId` links to the movement that proves it) |
| `Assets.AssetTagNumber` | Now `nvarchar(50)` with a unique index |
| `Facilities.IsCentralStore` | Marks the central store (CMS) |
| `AssetMovement.BatchId` / `PatientId` | Batch grouping and patient link |
| Sequences `AssetTagSeq`, `CylinderOrderSeq` | Asset tag numbers and order numbers |

New foreign keys use `Restrict`, so deleting a lookup, user or facility never cascades into these tables.

## Tests

```
dotnet test EMMS.Tests
```

`EMMS.Tests` (xUnit) is new; the repository had no automated tests before this. It has 81 tests:
- Code 128 encoding vectors;
- tag generation, including concurrent batches;
- serial clean-up and duplicate detection;
- the cylinder rules;
- **the order of the queue scan checks** (each check is tested against an asset that also fails every later check);
- dispatch checks, order status derivation and the Dispatch/Return reason rules.

Each phase was also checked against a **restored copy** of the development database, using real HTTP requests through the app:
- every scan code;
- a database error injected partway through Submit Batch (no partial batch; queue unchanged);
- the request → order → dispatch → receive flow, including a short delivery;
- a before/after HTML comparison of the existing screens. Only additions appear, and no existing markup was removed.

The browser-side behaviour hasn't been tried by a person yet: beeps, the camera, live counters, ticking on Scan to Receive, and the popups.

## Not done yet

- **Phase 4:**
  - one shared "current location" view and one "in transit" view;
  - cylinder stock dashboards by facility and by size/content;
  - requested vs dispatched vs received reports.
- **Hiding cylinders from repair and service pickers** (U5), and stronger protection of patient data (U6).
- **Movement status and cancel instead of delete** (finding D3): an explicit status on movements, with cancel replacing the current hard delete (`deleteMovement`).
- **Login and role checks on older actions** (finding S1). Several existing POST actions, such as `SaveAssets`, `ApproveMovement` and `ReceiveAsset`, still have no login or role attribute, and `ReceiveAsset` trusts `ReceivedBy` from the form.
- `Docs/CylinderTracking/README.md` and `schema.sql` predate this work. Some points there are out of date (for example, serials are typed or scanned, never generated). This file and the migrations are the current reference.

## Where things live

| Area | Files |
|---|---|
| Stickers / barcode | `Utility/Code128.cs`, `Views/AssetManagement/stickers.cshtml`, `wwwroot/js/site.js` (`printSelectedStickers`, `emmsPostTags`) |
| Tags / serials | `Service/AssetTagService.cs`, `Data/Repository/AssetTagSequenceRepo.cs`, `Utility/SerialNumber.cs` |
| Cylinder registration | `Service/CylinderRules.cs`, `Models/AssetCylinderDetail.cs`, `Controllers/AssetManagementController.cs`, `Views/AssetManagement/*` |
| Scanner helpers | `wwwroot/js/site.js` (`data-scan-target`, `data-scan-input`, `emmsBeep`, `emmsOpenCameraScanner`) |
| Movement Queue / batches / receive | `Service/MovementQueueService.cs`, `Controllers/AssetMovementController.cs`, `Views/AssetMovement/Queue.cshtml`, `Batch.cshtml`, `BatchGatePass.cshtml`, `recieveAsset.cshtml` |
| Requests / orders | `Service/CylinderOrderService.cs`, `Controllers/CylinderController.cs`, `Views/Cylinder/Index.cshtml` |
| Lookup flags admin | `Views/Admin/_LookupFlags.cshtml` |
| Notifications | `Service/NotificationService.cs` (batch, cylinder request and receipt-discrepancy messages) |
