import type { IPrimitivePaneRenderer, IPrimitivePaneView, ISeriesPrimitive, PrimitiveHoveredItem, SeriesAttachedParameter, Time } from "lightweight-charts";
import type { PointSurface, ScenarioPointInteraction } from "../tools/scenarioPoints";

type RenderTarget = Parameters<IPrimitivePaneRenderer["draw"]>[0];

/** The id the chart reports for a hovered handle of a named point. */
export const SCENARIO_HANDLE_ID = "scenarioHandle";

const RADIUS = 5;
const DEFAULT_COLOUR = "#2962FF";

/**
 * The handles of the named points of the level shown (SC10): a ring in the colour of the point's role, filled when
 * selected. Drawn on top, so a point can be taken even where a drawing lies over it.
 */
export class ScenarioHandles implements ISeriesPrimitive<Time> {
  private requestUpdate: (() => void) | undefined;
  private interaction: ScenarioPointInteraction | undefined;

  private readonly view: IPrimitivePaneView = {
    zOrder: () => "top",
    renderer: () => ({ draw: (target: RenderTarget) => this.draw(target) }),
  };

  constructor(
    private readonly surface: PointSurface,
    private readonly background: () => string,
  ) {}

  bind(interaction: ScenarioPointInteraction): void {
    this.interaction = interaction;
  }

  attached(parameters: SeriesAttachedParameter<Time>): void {
    this.requestUpdate = parameters.requestUpdate;
  }

  detached(): void {
    this.requestUpdate = undefined;
  }

  redraw(): void {
    this.requestUpdate?.();
  }

  paneViews(): readonly IPrimitivePaneView[] {
    return [this.view];
  }

  hitTest(x: number, y: number): PrimitiveHoveredItem | null {
    const cursor = this.interaction?.cursorAt({ x, y });
    return cursor === undefined ? null : { externalId: SCENARIO_HANDLE_ID, zOrder: "top", cursorStyle: cursor };
  }

  private draw(target: RenderTarget): void {
    const interaction = this.interaction;
    if (interaction === undefined) {
      return;
    }

    target.useMediaCoordinateSpace((scope) => {
      const context = scope.context;
      for (const { handle, at, selected } of interaction.visible()) {
        const point = this.surface.pointOf(at);
        if (point === undefined) {
          continue;
        }

        const colour = handle.colour ?? DEFAULT_COLOUR;
        context.beginPath();
        context.arc(point.x, point.y, selected ? RADIUS + 1 : RADIUS, 0, 2 * Math.PI);
        context.fillStyle = selected ? colour : this.background();
        context.fill();
        context.lineWidth = 2;
        context.strokeStyle = colour;
        context.stroke();
      }
    });
  }
}
