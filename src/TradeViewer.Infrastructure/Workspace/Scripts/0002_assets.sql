-- The assets of the user (AS1-AS5). They refer to market data by source and symbol only.
CREATE TABLE assets (
    source            TEXT NOT NULL,
    symbol            TEXT NOT NULL,
    display_name      TEXT NOT NULL,
    asset_class       TEXT NOT NULL,      -- crypto, stock, etf, forex, commodity; also the watchlist
    tick_size         REAL NOT NULL,
    session_type      TEXT NOT NULL,      -- continuous, exchange, newYorkClose
    session_time_zone TEXT NOT NULL,
    session_open      TEXT,               -- HH:mm, exchange sessions only
    session_close     TEXT,
    source_timeframes TEXT NOT NULL,      -- what the source provides, codes separated by commas
    position          INTEGER NOT NULL,   -- the place in its watchlist
    subscription_sent INTEGER NOT NULL,   -- 0: the Downloader has not accepted the current history yet
    added_at          TEXT NOT NULL,      -- ISO 8601, UTC
    PRIMARY KEY (source, symbol)
);

-- The history wanted per timeframe (AS3). history_from: first day (yyyy-MM-dd, UTC); NULL = complete history.
CREATE TABLE asset_history (
    source       TEXT NOT NULL,
    symbol       TEXT NOT NULL,
    timeframe    TEXT NOT NULL,
    history_from TEXT,
    PRIMARY KEY (source, symbol, timeframe),
    FOREIGN KEY (source, symbol) REFERENCES assets (source, symbol) ON DELETE CASCADE
);

-- Removed assets whose subscription the Downloader could not end yet (AS5); the sync sends it again.
CREATE TABLE pending_unsubscriptions (
    source       TEXT NOT NULL,
    symbol       TEXT NOT NULL,
    requested_at TEXT NOT NULL,
    PRIMARY KEY (source, symbol)
);
