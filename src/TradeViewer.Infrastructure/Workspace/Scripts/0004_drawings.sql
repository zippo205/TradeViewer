-- The drawings of the user (DR1-DR5). A drawing belongs to an asset, not to a timeframe (DR3), and goes
-- with its asset (AS5). The id is a UUID from the client, so a save can be repeated (WS1). tool and
-- payload_version say how to read the JSON payload; seq keeps the order in which drawings were made
-- (the later one is drawn on top).
CREATE TABLE drawings (
    id              TEXT NOT NULL PRIMARY KEY,
    source          TEXT NOT NULL,
    symbol          TEXT NOT NULL,
    tool            TEXT NOT NULL,      -- trendLine, ray, ... (DrawingCodes)
    payload_version INTEGER NOT NULL,
    payload         TEXT NOT NULL,
    seq             INTEGER NOT NULL,
    updated_at      TEXT NOT NULL,      -- ISO 8601, UTC
    FOREIGN KEY (source, symbol) REFERENCES assets (source, symbol) ON DELETE CASCADE
);

CREATE INDEX drawings_by_asset ON drawings (source, symbol, seq);

-- The Fibonacci presets (DR4). A Fibonacci drawing copies the levels of its preset, so editing or
-- deleting a preset never changes a drawing.
CREATE TABLE fibonacci_presets (
    id         TEXT NOT NULL PRIMARY KEY,
    name       TEXT NOT NULL,
    kind       TEXT NOT NULL,           -- retracement, extension
    levels     TEXT NOT NULL,           -- JSON array of ratios
    position   INTEGER NOT NULL,
    updated_at TEXT NOT NULL
);

-- The two defaults of DR4 (FibonacciPresets in the Domain; a test keeps both the same).
INSERT INTO fibonacci_presets (id, name, kind, levels, position, updated_at) VALUES
    ('6c0f3b1e-5d2a-4c1b-9a7e-000000000001', 'Retracement', 'retracement', '[0,0.236,0.382,0.5,0.618,0.786,1]', 1, '2026-11-17T00:00:00Z'),
    ('6c0f3b1e-5d2a-4c1b-9a7e-000000000002', 'Extension', 'extension', '[1,1.272,1.618,2,2.618,3.618]', 2, '2026-11-17T00:00:00Z');
