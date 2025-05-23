-- Insert LookupLists for MoveAsset fields
INSERT INTO LookupLists (Name, RowState) VALUES
('Movement Type', 1),
('Service Point', 1),
('Reason', 1),
('Functional Status', 1),
('Condition', 1);

-- Get the inserted LookupListIds
DECLARE @MovementTypeId INT = (SELECT LookupListId FROM LookupLists WHERE Name = 'Movement Type');
DECLARE @ServicePointId INT = (SELECT LookupListId FROM LookupLists WHERE Name = 'Service Point');
DECLARE @ReasonId INT = (SELECT LookupListId FROM LookupLists WHERE Name = 'Reason');
DECLARE @FunctionalStatusId INT = (SELECT LookupListId FROM LookupLists WHERE Name = 'Functional Status');
DECLARE @ConditionId INT = (SELECT LookupListId FROM LookupLists WHERE Name = 'Condition');

-- Insert LookupItems for each list

-- Movement Type
INSERT INTO LookupItems (LookupListId, Name, SortIndex, RowState) VALUES
(@MovementTypeId, 'To Facility', 1, 1),
(@MovementTypeId, 'To Service Point', 2, 1)

-- Service Point
INSERT INTO LookupItems (LookupListId, Name, SortIndex, RowState) VALUES
(@ServicePointId, 'OPD', 1, 1),
(@ServicePointId, 'Ward', 2, 1),
(@ServicePointId, 'ICU', 3, 1),
(@ServicePointId, 'Radiology', 4, 1);

-- Reason
INSERT INTO LookupItems (LookupListId, Name, SortIndex, RowState) VALUES
(@ReasonId, 'Routine Transfer', 1, 1),
(@ReasonId, 'Repair', 2, 1),
(@ReasonId, 'Replacement', 3, 1),
(@ReasonId, 'Upgrade', 4, 1),
(@ReasonId, 'Decommision', 5, 1),
(@ReasonId, 'Other', 6, 1);

-- Functional Status
INSERT INTO LookupItems (LookupListId, Name, SortIndex, RowState) VALUES
(@FunctionalStatusId, 'Functional', 1, 1),
(@FunctionalStatusId, 'Non-Functional', 2, 1),
(@FunctionalStatusId, 'Under Maintenance', 3, 1);

-- Condition
INSERT INTO LookupItems (LookupListId, Name, SortIndex, RowState) VALUES
(@ConditionId, 'New', 1, 1),
(@ConditionId, 'Good', 2, 1),
(@ConditionId, 'Fair', 3, 1),
(@ConditionId, 'Poor', 4, 1),
(@ConditionId, 'Scrap', 5, 1);