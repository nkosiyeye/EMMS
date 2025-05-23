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

-- Insert LookupItems for each list
INSERT INTO LookupItems (LookupListId, Name, SortIndex, RowState) VALUES
-- Categories
(@CategoryId, 'Medical Equipment', 1, 1),
(@CategoryId, 'Non Medical Equipment', 2, 1),

-- SubCategories
(@SubCategoryId, 'CT Scanner', 1, 1),
(@SubCategoryId, 'Centrifuge', 2, 1),
(@SubCategoryId, 'Ultrasound Scanner', 3, 1),
(@SubCategoryId, 'Oxygen Concentrator', 4, 1),
(@SubCategoryId, 'Ventilator', 5, 1),
(@SubCategoryId, 'Defibrillator', 6, 1),
(@SubCategoryId, 'Dialysis Machine', 7, 1),
(@SubCategoryId, 'Patient Monitor', 8, 1),
(@SubCategoryId, 'Infusion Pump', 9, 1),

-- Departments
(@DepartmentId, 'Lab', 1, 1),
(@DepartmentId, 'Radiology', 2, 1),
(@DepartmentId, 'ICU', 3, 1),
(@DepartmentId, 'OT', 4, 1),
(@DepartmentId, 'Ward', 5, 1),
(@DepartmentId, 'Pharmacy', 6, 1),
(@DepartmentId, 'Blood Bank', 7, 1),

-- Manufacturers
(@ManufacturerId, 'GE', 1, 1),
(@ManufacturerId, 'Philips', 2, 1),
(@ManufacturerId, 'Siemens', 3, 1),
(@ManufacturerId, 'Samsung', 4, 1),
(@ManufacturerId, 'Medtronic', 5, 1),
(@ManufacturerId, 'Fujifilm', 6, 1),

-- Vendors
(@VendorId, 'MOH', 1, 1),
(@VendorId, 'PERPFAR', 2, 1),
(@VendorId, 'ICAP', 3, 1),

-- Service Providers
(@ServiceProviderId, 'ASD', 1, 1),
(@ServiceProviderId, 'AVOMA', 2, 1),
(@ServiceProviderId, 'E-Medical', 3, 1),

-- Statuses
(@StatusId, 'New', 1, 1),
(@StatusId, 'Used', 2, 1),
(@StatusId, 'Refurbished', 3, 1),
(@StatusId, 'Decommissioned', 4, 1);


