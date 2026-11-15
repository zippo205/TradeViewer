-- Marks the file as a TradeViewer workspace ("TVWS" = 0x54565753); a restore refuses any other SQLite file.
PRAGMA application_id = 1414944595;

-- What is not part of a chart (WS5). One row per setting; the value is JSON.
CREATE TABLE settings (
    key        TEXT NOT NULL PRIMARY KEY,
    value      TEXT NOT NULL,
    updated_at TEXT NOT NULL   -- ISO 8601, UTC
);
