

-- Get the inserted LookupListIds
DECLARE @CategoryId INT = (SELECT LookupListId FROM LookupLists WHERE Name = 'Category');
DECLARE @SubCategoryId INT = (SELECT LookupListId FROM LookupLists WHERE Name = 'SubCategory');

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
(@SubCategoryId, 'UPS', 12, 1, @NonMedicalCategoryId);


