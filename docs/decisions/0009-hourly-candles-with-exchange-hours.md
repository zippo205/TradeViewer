# 0009 - Hourly Candles of Assets with Exchange Hours

Status: accepted (2026-11-18, owner). Adds to 0008; 0008 stays as written.

## Context

Decision 0008 left out 4h for stocks and ETFs because a trading day of 6.5 hours (09:30 to 16:00
New York time at US exchanges) splits into a 4-hour candle and a 2.5-hour one. The same holds,
smaller, for 1h: the candles open at 09:30, 10:30 ... 14:30, and the last one runs from 15:30 to
16:00, half an hour. 15m divides the day evenly (26 candles), and so do 1d and 1w.

The sources deliver 1h like that (yahoo's 60m), and the charting platforms show it like that:
TradingView draws seven hourly candles a day, the last of 30 minutes, and calculates its
indicators on them as drawn. Indicators count candles, not hours, and their usual settings were
used on such sessions for decades.

## Decision

1. **1h stays for assets with exchange hours**, as the source delivers it: six candles of an hour
   and a last one of half an hour per trading day. TradeViewer neither leaves it out nor reshapes it.
2. Indicators are calculated on these candles as they are (IN1), as in TradingView.
3. **EWTS** is told that the last 1h candle of a trading day covers half the time: where its
   analysis reads the size of a candle on 1h (range, ATR, volume, wicks), it has to know that. 1h
   is no context or setup timeframe (docs/knowledge/ewts-interface.md).

## Rejected

- Leaving out 1h as well: one candle in seven is short, against one in two for 4h; over the lengths
  of the indicators it hardly counts, and the intraday view of a stock would be 15m alone.
- Joining the last half hour to the candle before it (a 90-minute candle): no platform does it, so
  the chart would differ from what the user compares it with.

## Consequences

- Nothing to build: 1h comes from the source when the stock sources arrive.
- What decides is the trading hours of the asset, as in 0008.
