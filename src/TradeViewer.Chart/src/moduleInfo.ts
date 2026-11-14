import { name, version } from "../package.json";

/** What the module is and which interop contract it speaks (architecture, chapter 10). */
export interface ModuleInfo {
  readonly name: string;
  readonly version: string;
}

export function moduleInfo(): ModuleInfo {
  return { name, version };
}
