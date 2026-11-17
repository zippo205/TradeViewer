-- An asset keeps its ticker at the source when it differs from its symbol, the folder name (AS2): yahoo
-- EURUSD=X for EURUSD, tradingview NYSE:BRK.B for BRK-B. NULL means the same as the symbol, as for every
-- asset added before (binance). Lookups and subscriptions use the ticker (AS7).
ALTER TABLE assets ADD COLUMN source_symbol TEXT;
