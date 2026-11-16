/** The colours of a chart. Rising green and falling red as the rest of the application uses them. */
export interface ChartTheme {
  readonly background: string;
  readonly text: string;
  readonly grid: string;
  readonly border: string;
  readonly up: string;
  readonly down: string;
  /** The current candle, still in progress, is drawn paler (MD8). */
  readonly upPartial: string;
  readonly downPartial: string;
  /** Volume is drawn pale so that the candles above it stay readable (CH2). */
  readonly upVolume: string;
  readonly downVolume: string;
  readonly upVolumePartial: string;
  readonly downVolumePartial: string;
}

const UP = "8, 153, 129"; // #089981
const DOWN = "242, 54, 69"; // #f23645

const shared = {
  up: `rgb(${UP})`,
  down: `rgb(${DOWN})`,
  upPartial: `rgba(${UP}, 0.45)`,
  downPartial: `rgba(${DOWN}, 0.45)`,
  upVolume: `rgba(${UP}, 0.4)`,
  downVolume: `rgba(${DOWN}, 0.4)`,
  upVolumePartial: `rgba(${UP}, 0.18)`,
  downVolumePartial: `rgba(${DOWN}, 0.18)`,
};

const light: ChartTheme = { ...shared, background: "#ffffff", text: "#131722", grid: "#f0f3fa", border: "#d1d5db" };
const dark: ChartTheme = { ...shared, background: "#131722", text: "#d1d4dc", grid: "#1e222d", border: "#363a45" };

export function themeOf(isDark: boolean): ChartTheme {
  return isDark ? dark : light;
}
