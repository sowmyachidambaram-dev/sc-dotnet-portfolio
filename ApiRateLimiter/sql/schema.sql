-- Run this once against your SQL Server database.

CREATE TABLE dbo.RateLimitCounters (
    ClientId      NVARCHAR(256)  NOT NULL,
    WindowStart   DATETIME2      NOT NULL,
    RequestCount  INT            NOT NULL DEFAULT 1,
    ExpiresAt     DATETIME2      NOT NULL,
    CONSTRAINT PK_RateLimitCounters PRIMARY KEY (ClientId)
);

GO

-- Atomically increments the counter for a client, resetting the window if expired.
-- Returns the current RequestCount and ExpiresAt so the caller can decide allow/deny.
CREATE OR ALTER PROCEDURE dbo.usp_CheckAndIncrement
    @ClientId      NVARCHAR(256),
    @WindowSeconds INT,
    @Limit         INT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Now       DATETIME2 = SYSUTCDATETIME();
    DECLARE @ExpiresAt DATETIME2 = DATEADD(SECOND, @WindowSeconds, @Now);

    -- HOLDLOCK + MERGE guarantees a single atomic read-modify-write with no lost updates.
    MERGE dbo.RateLimitCounters WITH (HOLDLOCK) AS target
    USING (SELECT @ClientId AS ClientId) AS src
        ON target.ClientId = src.ClientId
    WHEN MATCHED AND target.ExpiresAt <= @Now THEN
        -- Window expired: reset the row.
        UPDATE SET WindowStart  = @Now,
                   RequestCount = 1,
                   ExpiresAt    = @ExpiresAt
    WHEN MATCHED THEN
        -- Same window: increment.
        UPDATE SET RequestCount = target.RequestCount + 1
    WHEN NOT MATCHED THEN
        INSERT (ClientId, WindowStart, RequestCount, ExpiresAt)
        VALUES (@ClientId, @Now, 1, @ExpiresAt);

    SELECT RequestCount, ExpiresAt
    FROM dbo.RateLimitCounters
    WHERE ClientId = @ClientId;
END;

GO

-- Azure Function / SQL Agent job calls this to keep the table lean.
CREATE OR ALTER PROCEDURE dbo.usp_PurgeExpiredCounters
AS
BEGIN
    SET NOCOUNT ON;
    DELETE FROM dbo.RateLimitCounters WHERE ExpiresAt < SYSUTCDATETIME();
END;
