USE EMMS;

-- Insert additional LookupLists for WorkRequest
INSERT INTO LookupLists (Name, RowState) VALUES
('WorkRequestFaultReport', 1),
('WorkRequestStatus', 1),
('WorkRequestOutcome', 1);

-- Get the inserted LookupListIds
DECLARE @TitleId INT = (SELECT LookupListId FROM LookupLists WHERE Name = 'WorkRequestFaultReport');
DECLARE @WorkRequestStatusId INT = (SELECT LookupListId FROM LookupLists WHERE Name = 'WorkRequestStatus');
DECLARE @OutcomeId INT = (SELECT LookupListId FROM LookupLists WHERE Name = 'WorkRequestOutcome');

-- Insert LookupItems for WorkRequest Title
INSERT INTO LookupItems (LookupListId, Name, SortIndex, RowState) VALUES
(@TitleId, 'Repair', 1, 1),
(@TitleId, 'Maintenance', 2, 1),
(@TitleId, 'Inspection', 3, 1),
(@TitleId, 'Other', 4, 1);

-- Insert LookupItems for WorkRequest Status
INSERT INTO LookupItems (LookupListId, Name, SortIndex, RowState) VALUES
(@WorkRequestStatusId, 'Open', 1, 1),
(@WorkRequestStatusId, 'In Progress', 2, 1),
(@WorkRequestStatusId, 'Completed', 3, 1),
(@WorkRequestStatusId, 'Cancelled', 4, 1),
(@WorkRequestStatusId, 'Closed', 5, 1);

-- Insert LookupItems for WorkRequest Outcome
INSERT INTO LookupItems (LookupListId, Name, SortIndex, RowState) VALUES
(@OutcomeId, 'Successful', 1, 1),
(@OutcomeId, 'Unsuccessful', 2, 1),
(@OutcomeId, 'Pending', 3, 1);
