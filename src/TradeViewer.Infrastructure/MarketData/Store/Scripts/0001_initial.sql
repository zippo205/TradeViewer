-- The candle store: a cache of the Downloader's files (decision 0003). Times are Unix seconds, UTC.

CREATE TABLE series (
    id        INTEGER PRIMARY KEY,
    source    TEXT NOT NULL,
    symbol    TEXT NOT NULL,
    timeframe TEXT NOT NULL,
    UNIQUE (source, symbol, timeframe)
);

CREATE TABLE candles (
    series_id INTEGER NOT NULL REFERENCES series (id) ON DELETE CASCADE,
    open_time INTEGER NOT NULL,
    open      REAL NOT NULL,
    high      REAL NOT NULL,
    low       REAL NOT NULL,
    close     REAL NOT NULL,
    volume    REAL NOT NULL,
    PRIMARY KEY (series_id, open_time)
) WITHOUT ROWID;

-- One row per file that was imported or refused.
CREATE TABLE import_log (
    series_id   INTEGER NOT NULL REFERENCES series (id) ON DELETE CASCADE,
    file        TEXT NOT NULL,
    size        INTEGER NOT NULL,
    modified    INTEGER NOT NULL,   -- Unix milliseconds
    candles     INTEGER NOT NULL,
    error       TEXT,               -- the message of the reader when the file broke the format rules (MD6)
    imported_at INTEGER NOT NULL,
    PRIMARY KEY (series_id, file)
) WITHOUT ROWID;

CREATE TABLE sync_state (
    source           TEXT NOT NULL,
    symbol           TEXT NOT NULL,
    last_change      INTEGER NOT NULL,
    last_status_json TEXT,
    checked_at       INTEGER NOT NULL,
    PRIMARY KEY (source, symbol)
) WITHOUT ROWID;
