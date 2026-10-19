-- Cylinder tracking integrated into the EMMS asset flow (SQL Server).
-- Existing tables assumed: Assets(AssetId), MoveAssets(MovementId), Facilities(FacilityId),
-- LookupLists(LookupListId), LookupItems(LookupItemId, LookupListId, Name, SortIndex, RowState, ParentId), Users(UserId).
-- Verify table/column names against ApplicationDbContext before running. Prefer EF migrations for real deployment.

-- 1. Lookup seed: Category 'Cylinder', SubCategory items, Size and Content lists ------------------------
DECLARE @CategoryListId    INT = (SELECT LookupListId FROM LookupLists WHERE Name = 'Category');
DECLARE @SubCategoryListId INT = (SELECT LookupListId FROM LookupLists WHERE Name = 'SubCategory');

INSERT INTO LookupLists (Name) VALUES ('CylinderSize'), ('CylinderContent');   -- add any other required columns
DECLARE @SizeListId    INT = (SELECT LookupListId FROM LookupLists WHERE Name = 'CylinderSize');
DECLARE @ContentListId INT = (SELECT LookupListId FROM LookupLists WHERE Name = 'CylinderContent');

INSERT INTO LookupItems (LookupListId, Name, SortIndex, RowState, ParentId)
VALUES (@CategoryListId, 'Cylinder', 3, 1, 0);
DECLARE @CylinderCategoryId INT = SCOPE_IDENTITY();

INSERT INTO LookupItems (LookupListId, Name, SortIndex, RowState, ParentId) VALUES
(@SubCategoryListId, 'Oxygen Cylinder', 1, 1, @CylinderCategoryId),
(@SizeListId,    '2 L',  1, 1, 0), (@SizeListId, '5 L', 2, 1, 0), (@SizeListId, '10 L', 3, 1, 0),   -- placeholder values
(@ContentListId, 'Oxygen', 1, 1, 0), (@ContentListId, 'Medical Air', 2, 1, 0);                    -- placeholder values

-- 2. Cylinder-specific asset fields (1:1 with Assets) ---------------------------------------------------
CREATE TABLE AssetCylinderDetails (
    AssetId    UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    SizeId     INT NOT NULL,
    ContentId  INT NOT NULL,
    CONSTRAINT FK_AssetCylDet_Asset   FOREIGN KEY (AssetId)   REFERENCES Assets(AssetId),
    CONSTRAINT FK_AssetCylDet_Size    FOREIGN KEY (SizeId)    REFERENCES LookupItems(LookupItemId),
    CONSTRAINT FK_AssetCylDet_Content FOREIGN KEY (ContentId) REFERENCES LookupItems(LookupItemId)
);

-- 3. Home based care patient (when movement service point is Home Based Care) ----------------------------
CREATE TABLE HomeBasedCarePatients (
    PatientId  UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    Number     NVARCHAR(50)  NOT NULL,
    Address    NVARCHAR(300) NOT NULL,
    Name       NVARCHAR(100) NOT NULL,
    Surname    NVARCHAR(100) NOT NULL
);

ALTER TABLE MoveAssets ADD PatientId UNIQUEIDENTIFIER NULL;
ALTER TABLE MoveAssets ADD CONSTRAINT FK_MoveAssets_Patient
    FOREIGN KEY (PatientId) REFERENCES HomeBasedCarePatients(PatientId);

-- 4. Request -> Order -> Dispatched cylinders ------------------------------------------------------------
CREATE TABLE CylinderRequests (
    RequestId          UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    FacilityId         INT NOT NULL,
    DescriptionId      INT NOT NULL,               -- LookupItem (dropdown)
    Quantity           INT NOT NULL CHECK (Quantity > 0),
    RequisitionName    NVARCHAR(150) NOT NULL,
    RequisitionNumber  NVARCHAR(50)  NOT NULL,
    Status             INT NOT NULL DEFAULT 0,     -- Requested, Approved, Rejected
    ApprovedBy         UNIQUEIDENTIFIER NULL,      -- facility pharmacist
    DateApproved       DATETIME2 NULL,
    CreatedBy UNIQUEIDENTIFIER NULL, DateCreated DATETIME2 NULL,
    ModifiedBy UNIQUEIDENTIFIER NULL, DateModified DATETIME2 NULL,
    RowState INT NOT NULL DEFAULT 0,
    CONSTRAINT FK_CylReq_Facility    FOREIGN KEY (FacilityId)    REFERENCES Facilities(FacilityId),
    CONSTRAINT FK_CylReq_Description FOREIGN KEY (DescriptionId) REFERENCES LookupItems(LookupItemId)
);

CREATE TABLE CylinderOrders (
    OrderId      UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    RequestId    UNIQUEIDENTIFIER NOT NULL,
    OrderedBy    UNIQUEIDENTIFIER NOT NULL,        -- CMS user
    OrderNumber  NVARCHAR(50) NOT NULL,
    Quantity     INT NOT NULL CHECK (Quantity > 0),
    Status       INT NOT NULL DEFAULT 0,           -- Ordered, Modified, Dispatched, Received
    CreatedBy UNIQUEIDENTIFIER NULL, DateCreated DATETIME2 NULL,
    ModifiedBy UNIQUEIDENTIFIER NULL, DateModified DATETIME2 NULL,
    RowState INT NOT NULL DEFAULT 0,
    CONSTRAINT UQ_CylOrders_OrderNumber UNIQUE (OrderNumber),
    CONSTRAINT FK_CylOrders_Request FOREIGN KEY (RequestId) REFERENCES CylinderRequests(RequestId)
);

CREATE TABLE CylinderOrderItems (
    OrderItemId   UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    OrderId       UNIQUEIDENTIFIER NOT NULL,
    AssetId       UNIQUEIDENTIFIER NOT NULL,       -- the registered cylinder
    DateReceived  DATETIME2 NULL,
    CONSTRAINT FK_CylOrdItems_Order FOREIGN KEY (OrderId) REFERENCES CylinderOrders(OrderId),
    CONSTRAINT FK_CylOrdItems_Asset FOREIGN KEY (AssetId) REFERENCES Assets(AssetId)
);

CREATE INDEX IX_CylReq_Facility ON CylinderRequests (FacilityId, Status);
CREATE INDEX IX_CylOrdItems_Asset ON CylinderOrderItems (AssetId);
