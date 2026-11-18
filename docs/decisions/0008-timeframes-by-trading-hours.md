# 0008 - Timeframes by Trading Hours

Status: accepted (2026-11-18, owner).

## Context

The five timeframes (15m, 1h, 4h, 1d, 1w) come from crypto, which trades around the clock: every
candle of a timeframe covers the same span. Binance delivers all five. The sources that bring
stocks, ETFs and Forex later (yahoo, tradingview) do not: yahoo has no 4h. MD3 said that a
timeframe the source lacks is built from the largest fitting one, aligned to the session.

How an asset trades decides whether that makes sense:

- Forex trades around the clock from Sunday evening to Friday evening (New York close). A 4h
  candle built from 1h covers four hours like every other one.
- Stocks and ETFs trade in exchange hours, at US exchanges 09:30 to 16:00 New York time, 6.5 hours.
  Built along the session, a day has one 4h candle (09:30 to 13:30) and one of 2.5 hours (13:30 to
  16:00), as TradingView draws it. ETFs are traded on the exchange in the same hours; crypto ETFs
  (e.g. IBIT) too.

## Decision

1. **Crypto** shows the timeframes its source delivers (binance: all five).
2. **Forex** gets a timeframe its source lacks built from the largest fitting one, along its
   trading week (4h from 1h).
3. **Stocks and ETFs** (every asset with exchange hours) have no 4h: it is not built, and the
   chart's button for it is disabled. The other timeframes are those the source delivers; a
   missing 1w would be built from 1d along the trading week.
4. **Choosing another asset** keeps the timeframe when the asset has it; otherwise the chart shows
   the next larger timeframe the asset has, and without one the largest smaller one. An address
   that names a timeframe the asset does not have shows the message instead of a chart, as today.
5. **EWTS**: for an asset with exchange hours, 4h is neither a context nor a setup timeframe; the
   context timeframe is 1d. TradeViewer offers only 1d when the analysis of such an asset is
   switched on (EW3) and when a backtest of one is started (BT1).

## Rejected

- Building 4h for stocks and ETFs along the session, as TradingView does: every second candle covers
  2.5 hours, so ATR, volume and the structure of swings mix two candle sizes, and an analysis would
  read the short afternoon candle as a weak one.
- Showing "no data" in a timeframe the asset lacks and switching drawings and indicators off: both
  belong to the asset and show on every timeframe, so a larger timeframe shows them at once; a
  third state of the chart page would gain nothing.
- Keeping the switch to the largest timeframe the asset has (the rule before this decision): from
  4h it jumps to 1w, further than needed.

## Consequences

- Building timeframes (MD3) moves from the epic Downloader Monitoring to the one that brings the
  stock and Forex sources and their trading sessions (roadmap, "Later").
- The choice of the timeframe on an asset change (point 4) is built with Downloader Monitoring.
- EWTS has to know the trading hours of an asset to refuse 4h for one with exchange hours; the
  interface document says so (docs/knowledge/ewts-interface.md) and the owner raises it with EWTS.
- The asset class alone does not decide it: what counts are the trading hours (the session of the
  asset, architecture chapter 5).
