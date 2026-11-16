-- The settings of a chart, saved per asset (CP2). The payload is JSON; payload_version lets a later
-- build read what an earlier one wrote. They go with their asset (AS5).
CREATE TABLE chart_settings (
    source          TEXT NOT NULL,
    symbol          TEXT NOT NULL,
    payload_version INTEGER NOT NULL,
    payload         TEXT NOT NULL,
    updated_at      TEXT NOT NULL,      -- ISO 8601, UTC
    PRIMARY KEY (source, symbol),
    FOREIGN KEY (source, symbol) REFERENCES assets (source, symbol) ON DELETE CASCADE
);
