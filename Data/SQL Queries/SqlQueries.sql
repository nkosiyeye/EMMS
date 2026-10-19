USE EMMS;
GO

-- Drop existing procedures if they exist
DROP PROCEDURE IF EXISTS sp_GetAssetsByFacility;
DROP PROCEDURE IF EXISTS sp_GetAssetsDueService;
DROP PROCEDURE IF EXISTS sp_GetOpenWorkRequestsByFacility;
DROP PROCEDURE IF EXISTS sp_GetOpenInfraWorkRequestsByFacility;
DROP PROCEDURE IF EXISTS sp_GetNotificationsByFacility;
DROP PROCEDURE IF EXISTS sp_GetAssetIndexViewModel;
DROP PROCEDURE IF EXISTS sp_GetAssetDueServiceViewModel;
DROP PROCEDURE IF EXISTS sp_GetJobsCount;
GO

------------------------------------------------------------
-- sp_GetAssetsByFacility
------------------------------------------------------------
CREATE PROCEDURE sp_GetAssetsByFacility
    @FacilityId INT = NULL
AS
BEGIN
    -- Temporary table for latest asset movement
    IF OBJECT_ID('tempdb..#AssetMovement') IS NOT NULL
        DROP TABLE #AssetMovement;

    SELECT *,
           ROW_NUMBER() OVER (PARTITION BY AssetId ORDER BY MovementDate DESC) AS VisitMovement
    INTO #AssetMovement
    FROM AssetMovement
    WHERE DateReceived IS NOT NULL;

    -- Select assets with latest movement
    SELECT a.*, (SELECT Name FROM LookupItems WHERE LookupItems.Id = a.SubCategoryId)  AS SubCategory
    FROM Assets a
    LEFT JOIN #AssetMovement am ON am.AssetId = a.AssetId AND am.VisitMovement = 1
	--INNER JOIN LookupItems SubCategory ON SubCategory.Id = a.SubCategoryId
    WHERE (a.RowState = 1) -- Active assets
      AND (@FacilityId IS NULL OR am.FacilityId = @FacilityId)
    ORDER BY am.DateCreated DESC;
END;
GO

------------------------------------------------------------
-- sp_GetAssetsByFacility
------------------------------------------------------------

CREATE PROCEDURE sp_GetAssetsDueService
    @Period INT = 4
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Today DATE = CAST(GETDATE() AS DATE);
    DECLARE @DueDate DATE = DATEADD(MONTH, @Period, @Today);

    SELECT a.*, SubCategory.Name  AS SubCategoryName
    FROM Assets a
	INNER JOIN LookupItems SubCategory ON SubCategory.Id = a.SubCategoryId
    WHERE a.NextServiceDate BETWEEN @Today AND @DueDate
    ORDER BY a.NextServiceDate DESC;
END;
GO
CREATE OR ALTER PROCEDURE sp_GetAssetMovements
AS
BEGIN
    SET NOCOUNT ON;

    ;WITH LatestMove AS (
        SELECT 
            m.*,
            ROW_NUMBER() OVER (PARTITION BY m.AssetId ORDER BY m.MovementDate DESC) AS rn
        FROM AssetMovement m
        WHERE m.DateReceived IS NOT NULL
    )
    SELECT 
        lm.*,
        f.FacilityName,
        sp.Name,
        receiveu.FirstName,
        receiveu.LastName
    FROM LatestMove lm
    INNER JOIN Facilities f ON f.FacilityId = lm.FromId
    LEFT JOIN LookupItems sp ON sp.Id = lm.ServicePointId
    LEFT JOIN [User] receiveu ON receiveu.UserId = lm.ReceivedBy
    LEFT JOIN [User] rejectu ON rejectu.UserId = lm.RejectedBy
    WHERE rn = 1
    ORDER BY lm.MovementDate DESC;
END;
GO

/*CREATE OR ALTER PROCEDURE sp_GetAssetMovements
AS
BEGIN
    SET NOCOUNT ON;

    SELECT 
        m.*,
        f.FacilityName,
        sp.Name,
        recieveu.FirstName,
        recieveu.LastName
        --rejectedu.FirstName,
        --rejectedu.LastName
    FROM AssetMovement m
    INNER JOIN Facilities f ON f.FacilityId = m.FromId
    LEFT JOIN LookupItems sp ON sp.Id = m.ServicePointId
    LEFT JOIN [User] recieveu ON recieveu.UserId = m.ReceivedBy 
    LEFT JOIN [User] rejectedu ON rejectedu.UserId = m.RejectedBy
    WHERE m.DateReceived IS NOT NULL
    ORDER BY m.MovementDate DESC;
END;
GO*/



------------------------------------------------------------
-- sp_GetOpenWorkRequestsByFacility
------------------------------------------------------------
CREATE PROCEDURE sp_GetOpenWorkRequestsByFacility
    @FacilityId INT = NULL
AS
BEGIN
    SELECT wr.*
    FROM WorkRequest wr
    INNER JOIN LookupItems li ON wr.WorkStatusId = li.Id
    WHERE wr.RowState = 1
      AND (@FacilityId IS NULL OR wr.FacilityId = @FacilityId)
      AND li.Name = 'Open'
    ORDER BY wr.DateCreated DESC;
END;
GO


------------------------------------------------------------
-- sp_GetOpenInfraWorkRequestsByFacility
------------------------------------------------------------
CREATE PROCEDURE sp_GetOpenInfraWorkRequestsByFacility
    @FacilityId INT = NULL
AS
BEGIN
    SELECT ir.*
    FROM InfrustructureWorkRequest ir
    INNER JOIN LookupItems li ON ir.WorkStatusId = li.Id
    WHERE ir.RowState = 1
      AND (@FacilityId IS NULL OR ir.FacilityId = @FacilityId)
      AND li.Name = 'Open'
    ORDER BY ir.DateCreated DESC;
END;
GO


------------------------------------------------------------
-- sp_GetNotificationsByFacility
------------------------------------------------------------
CREATE PROCEDURE sp_GetNotificationsByFacility
    @FacilityId INT = NULL,
    @Take INT = 5
AS
BEGIN
    SELECT TOP (@Take) *
    FROM Notifications
    WHERE RowState = 1
      AND (@FacilityId IS NULL OR FacilityId = @FacilityId)
    ORDER BY DateCreated DESC;
END;
GO


------------------------------------------------------------
-- sp_GetJobsCount
------------------------------------------------------------
CREATE PROCEDURE sp_GetJobsCount
    @FacilityId INT,
    @Completed BIT,
    @IsAdmin BIT
AS
BEGIN
    SELECT COUNT(*) AS JobCount
    FROM Job j
    INNER JOIN LookupItems li ON j.StatusId = li.Id
    WHERE j.RowState = 1
      AND ((@IsAdmin = 1) OR (j.FacilityId = @FacilityId))
      AND (
            (@Completed = 1 AND li.Name = 'Completed')
            OR (@Completed = 0 AND li.Name = 'In Progress')
          );
END;
GO
