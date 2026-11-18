-- Indicator templates (IN2): a named set of indicators. The payload is JSON (a list of indicators, as the
-- chart settings hold them); payload_version lets a later build read what an earlier one wrote. They belong to
-- no asset and survive the removal of every asset.
CREATE TABLE indicator_templates (
    id              TEXT NOT NULL PRIMARY KEY,
    name            TEXT NOT NULL UNIQUE COLLATE NOCASE,
    payload_version INTEGER NOT NULL,
    payload         TEXT NOT NULL,
    updated_at      TEXT NOT NULL      -- ISO 8601, UTC
);
