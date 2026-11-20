/// <reference types="vite/client" />

interface ImportMetaEnv {
  readonly VITE_API_BASE_URL?: string;
  /** Absolute origin shown in MCP/agent connection docs when different from the Studio page. */
  readonly VITE_PUBLIC_API_ORIGIN?: string;
  readonly VITE_TENANT_ID?: string;
  readonly VITE_API_TOKEN?: string;
  readonly VITE_PROJECT_ID?: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}
