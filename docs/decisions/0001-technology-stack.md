# 0001 - Technology Stack

Status: accepted (2026-11-11)

## Context

TradeViewer runs on a Raspberry Pi 5 in a home network without internet access. The chart is
the core of the product and must feel immediate. The owner is a .NET developer and maintains
EWTS and the Downloader on the same stack.

## Decision

- **Backend:** ASP.NET Core Minimal API on .NET 10, SDK pinned by `global.json`, C# language
  version 12. Same conventions as EWTS and the Downloader.
- **Frontend:** Blazor WebAssembly. The UI is written in C# and Razor.
- **Chart:** Lightweight Charts (Apache-2.0), bundled locally, plus our own TypeScript module
  for drawings and overlays. This is the only TypeScript in the solution. Node.js is needed at
  build time only.
- **Tests:** xUnit, NetArchTest, Vitest for the chart module.

## Reasons

- Blazor WebAssembly instead of Blazor Server: the UI runs in the browser, so there is no
  server-side session to lose on a Wi-Fi drop, and the Pi only serves data.
- Lightweight Charts renders candles, scales, zoom and pan fast on a canvas and needs no
  TradingView service or account. Its license requires an attribution notice, which the library
  shows by default.

## Rejected

- KLineChart: has built-in drawing tools, but gives less control and has a smaller community.
- Own canvas or WebGL chart: full control, but too much work before the first release.
- React or Angular: would move the whole UI to TypeScript.

## Consequences

- The drawing and interaction layer on top of Lightweight Charts is the largest single work
  package of the frontend.
- Large data never crosses the WebAssembly interop boundary; the chart module fetches candles
  itself (architecture, Chart Module).
