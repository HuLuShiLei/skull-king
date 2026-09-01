declare global {
  interface Window {
    __SKULLKING__?: { apiBase?: string }
  }
}

/** 未被 entrypoint 替换时 index.html 里留着的原样占位符。 */
const PLACEHOLDER = '__SKULLKING_API_BASE__'

/**
 * 后端根地址。空字符串表示同源，此时 `/api` 和 `/hub` 由反代按路径分流，
 * 这也是推荐的部署方式：不跨域，省掉 CORS 和跨站 Cookie 的一堆麻烦。
 */
export const apiBase: string = resolve()

function resolve(): string {
  const injected = window.__SKULLKING__?.apiBase

  if (!injected || injected === PLACEHOLDER) {
    return ''
  }

  // 末尾斜杠会拼出 //api 这种路径，统一去掉。
  return injected.replace(/\/+$/, '')
}
