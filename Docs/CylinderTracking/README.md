# Oxygen Cylinder Tracking – Design (integrated into EMMS asset flow)

Derived from the whiteboard sketches. Diagrams use Mermaid (render in VS Code / GitHub).

## Approach

Cylinders are **not a separate module**. They are `Asset` records, registered through the existing Equipment Registration form and moved with the existing Move Asset flow.

| Concern | Existing EMMS piece reused | What is added |
|---|---|---|
| Registration | `Asset` + `AssetRegistrationViewModel`; `Category` → `SubCategory` (Item Name) dropdowns backed by `LookupItem` | Category lookup item **Cylinder**, with SubCategory items (e.g. Oxygen Cylinder); cylinder-only fields **Size** and **Content** (dropdowns) |
| Serial number | `Asset.SerialNumber` (required) | Auto-generated for cylinders (hidden from the form, per sketch) |
| Movements | `MoveAsset` (facility → facility, service point, approval, receive) | Cylinder movement types; optional home-based-care patient |
| Ordering | – | New: `CylinderRequest`, `CylinderOrder`, `CylinderOrderItem` |

## 1. Registration flow – category-driven form

```mermaid
flowchart TD
    A([Start: Equipment Registration]) --> B[Select Category dropdown]
    B --> C{Category = Cylinder?}
    C -- No --> D[Existing asset form<br/>model, manufacturer, vendor, warranty,<br/>service interval, cost ...]
    C -- Yes --> E[Cylinder form<br/>Item Name, Size dropdown, Content dropdown<br/>serial number auto-generated]
    D --> F[Save Asset]
    E --> G[Save Asset + AssetCylinderDetail]
    F --> H([Asset registered])
    G --> H
```

Sketch of the form fields for the Cylinder category:

```
Equipment ▸ Category: Cylinder ▸ Item Name: [dropdown]
 ├─ Id                (system)
 ├─ Serial number     (struck out on sketch → auto-generated, unique)
 ├─ Barcode           (struck out → not captured)
 ├─ Size              [dropdown]
 ├─ Expiry            (struck out → not captured)
 └─ Content           [dropdown]
```

## 2. Request, approval and dispatch flow

```mermaid
flowchart TD
    A([Start: signed-in facility user]) --> B[Fill Cylinder Request Form<br/>facility, description, quantity,<br/>requisition name/number]
    B --> C{Facility pharmacist<br/>approves?}
    C -- Rejected --> B
    C -- Approved --> D[CMS: Create Order<br/>order number, quantity]
    D --> E{Plant status}
    E -- Plant down --> B
    E -- Plant up --> F{CMS approval}
    F -- Modify --> G[Order modified]
    G --> F
    F -- Approved --> H[Dispatch: pick registered cylinder Assets<br/>create Move Asset CMS to Facility]
    H --> I([Facility receives cylinders])
```

## 3. Cylinder movement flow (uses Move Asset)

```mermaid
flowchart LR
    CMS[(CMS)] -- Dispatch --> FAC1[Facility A]
    FAC1 -- Move --> SP[Service point]
    SP -. Home Based Care .-> HBC[Patient: number, address,<br/>name, surname]
    FAC1 -- Facility to facility --> FAC2[Facility B]
    FAC1 -- Return to CMS --> CMS
    FAC2 -- Return to CMS --> CMS
```

## 4. Order status

```mermaid
stateDiagram-v2
    [*] --> Requested
    Requested --> Approved: pharmacist approves
    Requested --> Rejected: pharmacist rejects
    Approved --> Ordered: CMS creates order
    Ordered --> Requested: plant down
    Ordered --> Modified: CMS modifies
    Modified --> Ordered
    Ordered --> Dispatched: CMS approves + dispatches
    Dispatched --> Received: facility confirms
    Received --> [*]
    Rejected --> [*]
```

## 5. ERD

Existing tables are shown with only the relevant columns. New tables are marked **(new)**.

```mermaid
erDiagram
    LOOKUP_ITEM ||--o{ ASSET : "category / sub-category"
    ASSET ||--o| ASSET_CYLINDER_DETAIL : "if category = Cylinder (new)"
    LOOKUP_ITEM ||--o{ ASSET_CYLINDER_DETAIL : "size, content"
    ASSET ||--o{ MOVE_ASSET : moves
    FACILITY ||--o{ MOVE_ASSET : "from / to"
    LOOKUP_ITEM ||--o{ MOVE_ASSET : "service point"
    HOME_BASED_CARE_PATIENT ||--o{ MOVE_ASSET : "receives (new)"
    FACILITY ||--o{ CYLINDER_REQUEST : raises
    CYLINDER_REQUEST ||--o| CYLINDER_ORDER : "becomes"
    CYLINDER_ORDER ||--o{ CYLINDER_ORDER_ITEM : dispatches
    ASSET ||--o{ CYLINDER_ORDER_ITEM : "allocated in"
    USER ||--o{ CYLINDER_REQUEST : requests
    USER ||--o{ CYLINDER_ORDER : creates

    ASSET {
        guid AssetId PK
        string SerialNumber "auto-generated for cylinders"
        int CategoryId FK
        int SubCategoryId FK
        int StatusId
    }
    ASSET_CYLINDER_DETAIL {
        guid AssetId PK,FK
        int SizeId FK
        int ContentId FK
    }
    MOVE_ASSET {
        guid MovementId PK
        guid AssetId FK
        int FromId FK
        int FacilityId FK
        int ServicePointId FK "nullable"
        int MovementType
        int Reason
        guid PatientId FK "nullable, HBC only"
    }
    HOME_BASED_CARE_PATIENT {
        guid PatientId PK
        string Number
        string Address
        string Name
        string Surname
    }
    CYLINDER_REQUEST {
        guid RequestId PK
        int FacilityId FK
        int DescriptionId FK
        int Quantity
        string RequisitionName
        string RequisitionNumber
        int Status
    }
    CYLINDER_ORDER {
        guid OrderId PK
        guid RequestId FK
        guid OrderedBy FK
        string OrderNumber UK
        int Quantity
        int Status
    }
    CYLINDER_ORDER_ITEM {
        guid OrderItemId PK
        guid OrderId FK
        guid AssetId FK
        datetime DateReceived "nullable"
    }
```

## 6. Schema

See [schema.sql](schema.sql): lookup seed data, new tables, and one `ALTER` on `MoveAssets`.

## 7. Assumptions to confirm

- **Cylinder-specific fields** are held in a 1:1 extension table (`AssetCylinderDetails`) so `Assets` stays unchanged. Alternative: nullable `SizeId` / `ContentId` on `Assets`.
- **Asset fields hidden for cylinders**: model, manufacturer, vendor, service provider, placement, warranty, service interval. Existing `[Required]` attributes on `Asset` must become conditional on category (like the current `RequiredIf` pattern), or receive defaults for cylinders.
- **Size / Content** are new `LookupList`s (e.g. sizes 2 L / 5 L / 10 L; content Oxygen / Medical Air). Values are placeholders.
- **CMS is a `Facility` row**, so dispatch and return-to-CMS are ordinary Move Asset records. If it is not, `FromId`/`FacilityId` need a null-means-CMS convention.
- **Ordered cylinders must already be registered** as assets; dispatch selects specific serial-numbered cylinders.
- **"Plant down"** = CMS cannot fill the order; it returns to the request form.
- **Second diamond** = CMS approval with outcomes Modify (loops back) and Approved (dispatch).
- The sketch's movement fields were partly illegible; the existing `MoveAsset` fields are used instead.
