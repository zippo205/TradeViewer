import { describe, expect, it } from "vitest";
import { CandleRequestError, candlesAddress, fetchCandles } from "./candleClient";

const key = { source: "binance", symbol: "BTCUSDT", timeframe: "1h" };

function answer(status: number, body: unknown): typeof fetch {
  return async () =>
    new Response(typeof body === "string" ? body : JSON.stringify(body), {
      status,
      headers: { "Content-Type": status === 200 ? "application/json" : "application/problem+json" },
    });
}

describe("candlesAddress", () => {
  it("asks for the latest page without a time", () => {
    expect(candlesAddress(key, undefined, 2000)).toBe("api/candles/binance/BTCUSDT/1h?limit=2000");
  });

  it("asks for the page before a time while scrolling back", () => {
    expect(candlesAddress(key, 1735603200, 5000)).toBe("api/candles/binance/BTCUSDT/1h?limit=5000&to=1735603200");
  });

  it("escapes names", () => {
    expect(candlesAddress({ source: "yahoo", symbol: "EURUSD=X", timeframe: "1d" }, undefined, 10)).toBe(
      "api/candles/yahoo/EURUSD%3DX/1d?limit=10",
    );
  });
});

describe("fetchCandles", () => {
  it("returns the candles of the page", async () => {
    const columns = { t: [900], o: [1], h: [3], l: [0.5], c: [2], v: [10], partial: false };

    const page = await fetchCandles(key, undefined, 2000, answer(200, columns));

    expect(page).toEqual({ bars: [{ time: 900, open: 1, high: 3, low: 0.5, close: 2, volume: 10 }], partial: false });
  });

  it("turns problem details into an error with their message", async () => {
    const problem = { title: "Invalid data", status: 422, detail: "binance/BTCUSDT has no timeframe 7m." };

    const error = await fetchCandles(key, undefined, 2000, answer(422, problem)).catch((caught: unknown) => caught);

    expect(error).toBeInstanceOf(CandleRequestError);
    expect((error as CandleRequestError).message).toBe("binance/BTCUSDT has no timeframe 7m.");
    expect((error as CandleRequestError).status).toBe(422);
  });

  it("names the status when the answer is no problem details", async () => {
    const error = await fetchCandles(key, undefined, 2000, answer(502, "<html>proxy</html>")).catch((caught: unknown) => caught);

    expect((error as CandleRequestError).message).toBe("The candles could not be read (502).");
  });

  it("says so when the server is not reachable", async () => {
    const failing: typeof fetch = async () => {
      throw new TypeError("Failed to fetch");
    };

    const error = await fetchCandles(key, undefined, 2000, failing).catch((caught: unknown) => caught);

    expect((error as CandleRequestError).message).toBe("The server is not reachable.");
    expect((error as CandleRequestError).status).toBe(0);
  });

  it("passes a cancelled request on as cancelled", async () => {
    const aborted: typeof fetch = async () => {
      throw new DOMException("The operation was aborted.", "AbortError");
    };

    const error = await fetchCandles(key, undefined, 2000, aborted).catch((caught: unknown) => caught);

    expect(error).toBeInstanceOf(DOMException);
    expect(error).not.toBeInstanceOf(CandleRequestError);
  });
});
