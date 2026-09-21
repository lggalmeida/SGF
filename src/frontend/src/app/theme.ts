export type Theme = 'light' | 'dark'

export const themeStorageKey = 'sgf.theme'

export function getTheme(): Theme {
  const applied = document.documentElement.dataset.theme
  if (applied === 'light' || applied === 'dark') return applied
  return window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light'
}

export function applyTheme(theme: Theme) {
  document.documentElement.dataset.theme = theme
  document.documentElement.style.colorScheme = theme
  try { localStorage.setItem(themeStorageKey, theme) } catch { /* The theme still applies for this page. */ }
}
