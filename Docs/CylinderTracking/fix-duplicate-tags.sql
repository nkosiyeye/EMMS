-- Finding D1 remediation: run ONLY if the AssetTagSequence migration stopped with error 50001.
-- Keeps the OLDEST asset on each duplicated tag (it is the one most likely to carry a printed label)
-- and gives every newer copy a fresh tag above the current highest suffix.
-- Step 1 is read-only. Review its output, reprint stickers for the re-tagged assets, then run step 2.

-- 1. Review --------------------------------------------------------------------------------------------
SELECT a.AssetTagNumber, a.AssetId, a.SerialNumber, a.DateCreated,
       ROW_NUMBER() OVER (PARTITION BY a.AssetTagNumber ORDER BY a.DateCreated, a.AssetId) AS CopyNo
FROM Assets a
WHERE a.AssetTagNumber IN (SELECT AssetTagNumber FROM Assets GROUP BY AssetTagNumber HAVING COUNT(*) > 1)
ORDER BY a.AssetTagNumber, CopyNo;

-- 2. Re-tag (uncomment to run) -------------------------------------------------------------------------
/*
BEGIN TRAN;

DECLARE @next bigint = 1 + ISNULL((
    SELECT MAX(TRY_CAST(CASE WHEN CHARINDEX('-', AssetTagNumber) > 0
                             THEN RIGHT(AssetTagNumber, CHARINDEX('-', REVERSE(AssetTagNumber)) - 1) END AS bigint))
    FROM Assets), 0);

WITH copies AS (
    SELECT a.AssetId, a.AssetTagNumber,
           ROW_NUMBER() OVER (PARTITION BY a.AssetTagNumber ORDER BY a.DateCreated, a.AssetId) AS CopyNo
    FROM Assets a
), retag AS (
    SELECT AssetId, AssetTagNumber,
           @next - 1 + ROW_NUMBER() OVER (ORDER BY AssetTagNumber, CopyNo) AS NewNumber
    FROM copies
    WHERE CopyNo > 1
)
UPDATE a
SET a.AssetTagNumber = LEFT(r.AssetTagNumber, LEN(r.AssetTagNumber) - CHARINDEX('-', REVERSE(r.AssetTagNumber)) + 1)
                       + RIGHT('000' + CAST(r.NewNumber AS varchar(20)), CASE WHEN LEN(r.NewNumber) < 3 THEN 3 ELSE LEN(r.NewNumber) END),
    a.DateModified = SYSDATETIME()
OUTPUT r.AssetTagNumber AS OldTag, inserted.AssetTagNumber AS NewTag, inserted.AssetId
FROM Assets a
JOIN retag r ON r.AssetId = a.AssetId;

COMMIT;
*/
