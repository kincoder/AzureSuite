-- Run against the deployed Catalog SQL database after migrations have been applied.
-- Idempotent: skips seeding if a client already exists.
-- Column shapes confirmed against the AddClient/AddRoute EF Core migrations:
--   Clients(Id uniqueidentifier, Name nvarchar(200), RegisteredAtUtc datetime2)
--   Routes(Id uniqueidentifier, ClientId uniqueidentifier, MessageTypeName nvarchar(100),
--          MessageTypeVersion nvarchar(20), QueueNames nvarchar(max) -- JSON array)

IF NOT EXISTS (SELECT 1 FROM Clients)
BEGIN
    DECLARE @ClientAId UNIQUEIDENTIFIER = NEWID();
    DECLARE @ClientBId UNIQUEIDENTIFIER = NEWID();

    INSERT INTO Clients (Id, Name, RegisteredAtUtc)
    VALUES
        (@ClientAId, 'Contoso Payments', SYSUTCDATETIME()),
        (@ClientBId, 'Fabrikam Treasury', SYSUTCDATETIME());

    INSERT INTO Routes (Id, ClientId, MessageTypeName, MessageTypeVersion, QueueNames)
    VALUES
        (NEWID(), @ClientAId, 'pacs.008', '1.0', N'["out-settlements"]'),
        (NEWID(), @ClientBId, 'pacs.008', '1.0', N'["out-settlements"]'),
        (NEWID(), @ClientBId, 'camt.054', '1.0', N'["out-notifications"]'),
        (NEWID(), @ClientBId, 'camt.054', '1.0', N'["out-audit"]');
END
