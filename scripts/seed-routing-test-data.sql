-- Run against a Catalog SQL database after migrations have been applied (local LocalDB or Azure SQL).
-- Idempotent: each message type is inserted only if missing; clients/routes are skipped if any client exists.
-- Message types go first: Routes has foreign keys to MessageTypes(Name, Version) and Clients(Id).
-- Column shapes confirmed against the InitialCreate/AddClient/AddRoute EF Core migrations:
--   MessageTypes(Id uniqueidentifier, Name nvarchar(100), Version nvarchar(20), SchemaDefinition nvarchar(max), RegisteredAtUtc datetime2)
--   Clients(Id uniqueidentifier, Name nvarchar(200), RegisteredAtUtc datetime2)
--   Routes(Id uniqueidentifier, ClientId uniqueidentifier, MessageTypeName nvarchar(100),
--          MessageTypeVersion nvarchar(20), QueueNames nvarchar(max) -- JSON array)

IF NOT EXISTS (SELECT 1 FROM MessageTypes WHERE Name = 'pacs.008' AND Version = '1.0')
    INSERT INTO MessageTypes (Id, Name, Version, SchemaDefinition, RegisteredAtUtc)
    VALUES (NEWID(), 'pacs.008', '1.0', N'{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "title": "pacs.008 FI to FI Customer Credit Transfer",
  "type": "object",
  "required": ["messageId", "creationDateTime", "amount", "debtor", "creditor"],
  "properties": {
    "messageId": { "type": "string", "maxLength": 35 },
    "creationDateTime": { "type": "string", "format": "date-time" },
    "amount": {
      "type": "object",
      "required": ["value", "currency"],
      "properties": {
        "value": { "type": "number", "exclusiveMinimum": 0 },
        "currency": { "type": "string", "pattern": "^[A-Z]{3}$" }
      }
    },
    "debtor": { "$ref": "#/$defs/party" },
    "creditor": { "$ref": "#/$defs/party" }
  },
  "$defs": {
    "party": {
      "type": "object",
      "required": ["name", "iban"],
      "properties": {
        "name": { "type": "string", "maxLength": 140 },
        "iban": { "type": "string", "pattern": "^[A-Z]{2}[0-9]{2}[A-Z0-9]{11,30}$" }
      }
    }
  }
}', SYSUTCDATETIME());

IF NOT EXISTS (SELECT 1 FROM MessageTypes WHERE Name = 'pacs.002' AND Version = '1.0')
    INSERT INTO MessageTypes (Id, Name, Version, SchemaDefinition, RegisteredAtUtc)
    VALUES (NEWID(), 'pacs.002', '1.0', N'{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "title": "pacs.002 Payment Status Report",
  "type": "object",
  "required": ["messageId", "originalMessageId", "status"],
  "properties": {
    "messageId": { "type": "string", "maxLength": 35 },
    "originalMessageId": { "type": "string", "maxLength": 35 },
    "status": { "enum": ["ACCP", "ACSC", "PDNG", "RJCT"] },
    "reasonCode": { "type": "string", "maxLength": 4 }
  }
}', SYSUTCDATETIME());

IF NOT EXISTS (SELECT 1 FROM MessageTypes WHERE Name = 'pain.001' AND Version = '1.0')
    INSERT INTO MessageTypes (Id, Name, Version, SchemaDefinition, RegisteredAtUtc)
    VALUES (NEWID(), 'pain.001', '1.0', N'{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "title": "pain.001 Customer Credit Transfer Initiation",
  "type": "object",
  "required": ["messageId", "initiatingParty", "payments"],
  "properties": {
    "messageId": { "type": "string", "maxLength": 35 },
    "initiatingParty": { "type": "string", "maxLength": 140 },
    "payments": {
      "type": "array",
      "minItems": 1,
      "items": {
        "type": "object",
        "required": ["endToEndId", "amount", "currency", "creditorIban"],
        "properties": {
          "endToEndId": { "type": "string", "maxLength": 35 },
          "amount": { "type": "number", "exclusiveMinimum": 0 },
          "currency": { "type": "string", "pattern": "^[A-Z]{3}$" },
          "creditorIban": { "type": "string", "pattern": "^[A-Z]{2}[0-9]{2}[A-Z0-9]{11,30}$" }
        }
      }
    }
  }
}', SYSUTCDATETIME());

IF NOT EXISTS (SELECT 1 FROM MessageTypes WHERE Name = 'camt.054' AND Version = '1.0')
    INSERT INTO MessageTypes (Id, Name, Version, SchemaDefinition, RegisteredAtUtc)
    VALUES (NEWID(), 'camt.054', '1.0', N'{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "title": "camt.054 Bank to Customer Debit/Credit Notification",
  "type": "object",
  "required": ["messageId", "accountIban", "entries"],
  "properties": {
    "messageId": { "type": "string", "maxLength": 35 },
    "accountIban": { "type": "string", "pattern": "^[A-Z]{2}[0-9]{2}[A-Z0-9]{11,30}$" },
    "entries": {
      "type": "array",
      "minItems": 1,
      "items": {
        "type": "object",
        "required": ["amount", "currency", "creditDebitIndicator", "bookingDate"],
        "properties": {
          "amount": { "type": "number", "exclusiveMinimum": 0 },
          "currency": { "type": "string", "pattern": "^[A-Z]{3}$" },
          "creditDebitIndicator": { "enum": ["CRDT", "DBIT"] },
          "bookingDate": { "type": "string", "format": "date" }
        }
      }
    }
  }
}', SYSUTCDATETIME());

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
