// Types — safe to re-export all (no runtime conflicts)
export type * from "./types";

// Runtime exports — named to avoid conflicts
export { buildCssVariableStyle } from "./branding";

export {
  loadAccountConfig,
  loadAccountContent,
  loadAgentConfig,
  loadAgentContent,
  loadLegalContent,
  getAgentIds,
} from "./config";

export {
  extractAgentId,
  resolveAgentFromCustomDomain,
  isWwwCustomDomain,
  getAgentIds as getAgentIdSet,
} from "./routing";

export { loadNavConfig } from "./nav-config";
