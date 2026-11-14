// Bundles the chart module into the wwwroot of TradeViewer.Web, where the Api serves it.
// Called by `npm run build`, and by the MSBuild target of TradeViewer.Web.
import { build } from "esbuild";

const production = process.env.CHART_CONFIGURATION === "Release";

await build({
  entryPoints: ["src/index.ts"],
  bundle: true,
  format: "esm",
  target: "es2022",
  outfile: "../TradeViewer.Web/wwwroot/chart/index.js",
  minify: production,
  sourcemap: !production,
  logLevel: "info",
});
