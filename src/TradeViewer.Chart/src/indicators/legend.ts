import { titleOf, type IndicatorInstance } from "./indicator";

/** What the user can do from the legend line of an indicator, as in TradingView. */
export type IndicatorAction = "hide" | "settings" | "remove";

/** One value of a legend line: its text and the colour of its line. */
export interface LegendValue {
  readonly text: string;
  readonly color: string;
}

const ACTIONS: { action: IndicatorAction; title: string; text: string }[] = [
  { action: "hide", title: "Hide", text: "◉" },
  { action: "settings", title: "Settings", text: "⚙" },
  { action: "remove", title: "Remove", text: "✕" },
];

/**
 * The legend line of one indicator (IN1): its title, its values under the crosshair (the latest without one) and
 * the actions hide, settings, remove. The line is an element the indicator set places at the top left of its pane.
 */
export class IndicatorLegend {
  readonly element: HTMLDivElement;
  /** The indicator of the line; the actions report it. */
  id: string;
  private readonly title: HTMLSpanElement;
  private readonly values: HTMLSpanElement;
  private readonly hideButton: HTMLButtonElement;

  constructor(
    instance: IndicatorInstance,
    onAction: (id: string, action: IndicatorAction) => void,
  ) {
    this.id = instance.id;
    this.element = document.createElement("div");
    this.element.className = "tv-indicator-legend";
    this.title = document.createElement("span");
    this.title.className = "tv-indicator-title";
    this.values = document.createElement("span");
    this.values.className = "tv-indicator-values";

    const actions = document.createElement("span");
    actions.className = "tv-indicator-actions";
    const buttons = ACTIONS.map(({ action, title, text }) => {
      const button = document.createElement("button");
      button.type = "button";
      button.title = title;
      button.textContent = text;
      button.dataset["action"] = action;
      // The press must not reach the chart below: it would pan or place a drawing.
      button.addEventListener("mousedown", (event) => event.stopPropagation());
      button.addEventListener("click", (event) => {
        event.stopPropagation();
        onAction(this.id, action);
      });
      return button;
    });
    this.hideButton = buttons[0]!;
    actions.append(...buttons);

    this.element.append(this.title, this.values, actions);
    this.update(instance);
  }

  update(instance: IndicatorInstance): void {
    this.id = instance.id;
    this.title.textContent = titleOf(instance);
    this.element.classList.toggle("tv-indicator-hidden", instance.hidden);
    this.hideButton.title = instance.hidden ? "Show" : "Hide";
    this.hideButton.textContent = instance.hidden ? "◎" : "◉";
  }

  show(values: readonly LegendValue[]): void {
    this.values.replaceChildren(
      ...values.map((value) => {
        const span = document.createElement("span");
        span.textContent = value.text;
        span.style.color = value.color;
        return span;
      }),
    );
  }
}
