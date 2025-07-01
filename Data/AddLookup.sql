INSERT INTO LookupLists (Name, RowState) VALUES
('Category', 1),
('SubCategory', 1),
('Department', 1),
('Manufacturer', 1),
('Vendor', 1),
('Service Provider', 1),
('Status', 1);

-- Get the inserted LookupListIds
DECLARE @CategoryId INT = (SELECT LookupListId FROM LookupLists WHERE Name = 'Category');
DECLARE @SubCategoryId INT = (SELECT LookupListId FROM LookupLists WHERE Name = 'SubCategory');
DECLARE @DepartmentId INT = (SELECT LookupListId FROM LookupLists WHERE Name = 'Department');
DECLARE @ManufacturerId INT = (SELECT LookupListId FROM LookupLists WHERE Name = 'Manufacturer');
DECLARE @VendorId INT = (SELECT LookupListId FROM LookupLists WHERE Name = 'Vendor');
DECLARE @ServiceProviderId INT = (SELECT LookupListId FROM LookupLists WHERE Name = 'Service Provider');
DECLARE @StatusId INT = (SELECT LookupListId FROM LookupLists WHERE Name = 'Status');

-- Insert Categories and capture their IDs
INSERT INTO LookupItems (LookupListId, Name, SortIndex, RowState, ParentId)
VALUES (@CategoryId, 'Medical Equipment', 1, 1, 0);
DECLARE @MedicalCategoryId INT = SCOPE_IDENTITY();

INSERT INTO LookupItems (LookupListId, Name, SortIndex, RowState, ParentId)
VALUES (@CategoryId, 'Non Medical Equipment', 2, 1, 0);
DECLARE @NonMedicalCategoryId INT = SCOPE_IDENTITY();

-- Insert SubCategories with ParentId
INSERT INTO LookupItems (LookupListId, Name, SortIndex, RowState, ParentId) VALUES
-- Medical Equipment Subcategories
(@SubCategoryId, 'CT Scanner', 1, 1, @MedicalCategoryId),
(@SubCategoryId, 'Centrifuge', 2, 1, @MedicalCategoryId),
(@SubCategoryId, 'Ultrasound Scanner', 3, 1, @MedicalCategoryId),
(@SubCategoryId, 'Oxygen Concentrator', 4, 1, @MedicalCategoryId),
(@SubCategoryId, 'Ventilator', 5, 1, @MedicalCategoryId),
(@SubCategoryId, 'Defibrillator', 6, 1, @MedicalCategoryId),
(@SubCategoryId, 'Dialysis Machine', 7, 1, @MedicalCategoryId),
(@SubCategoryId, 'Patient Monitor', 8, 1, @MedicalCategoryId),
(@SubCategoryId, 'Infusion Pump', 9, 1, @MedicalCategoryId),

-- Non Medical Equipment Subcategories (example)
(@SubCategoryId, 'Generator', 10, 1, @NonMedicalCategoryId),
(@SubCategoryId, 'Air Conditioner', 11, 1, @NonMedicalCategoryId),
(@SubCategoryId, 'UPS', 12, 1, @NonMedicalCategoryId),


-- Departments
(@DepartmentId, 'Lab', 1, 1, 0),
(@DepartmentId, 'Radiology', 2, 1, 0),
(@DepartmentId, 'ICU', 3, 1, 0),
(@DepartmentId, 'OT', 4, 1, 0),
(@DepartmentId, 'Ward', 5, 1, 0),
(@DepartmentId, 'Pharmacy', 6, 1, 0),
(@DepartmentId, 'Blood Bank', 7, 1, 0),

-- Manufacturers
(@ManufacturerId, 'GE', 1, 1, 0),
(@ManufacturerId, 'Philips', 2, 1, 0),
(@ManufacturerId, 'Siemens', 3, 1, 0),
(@ManufacturerId, 'Samsung', 4, 1, 0),
(@ManufacturerId, 'Medtronic', 5, 1, 0),
(@ManufacturerId, 'Fujifilm', 6, 1, 0),

-- Vendors
(@VendorId, 'MOH', 1, 1, 0),
(@VendorId, 'PERPFAR', 2, 1, 0),
(@VendorId, 'ICAP', 3, 1, 0),

-- Service Providers
(@ServiceProviderId, 'ASD', 1, 1, 0),
(@ServiceProviderId, 'AVOMA', 2, 1, 0),
(@ServiceProviderId, 'E-Medical', 3, 1, 0),

-- Statuses
(@StatusId, 'New', 1, 1, 0),
(@StatusId, 'Used', 2, 1, 0),
(@StatusId, 'Refurbished', 3, 1, 0),
(@StatusId, 'Decommissioned', 4, 1, 0);


