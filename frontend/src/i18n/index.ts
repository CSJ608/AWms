/**
 * i18n 初始化 —— 中英；语言持久化 localStorage；Accept-Language 由 client 层读取。
 */
import i18n from 'i18next'
import { initReactI18next } from 'react-i18next'
import { getLocalStorage } from '../lib/storage'
import en from './locales/en'
import zh from './locales/zh'

const LANG_KEY = 'awms.lang'

export const LANGUAGES = ['zh', 'en'] as const
export type Language = (typeof LANGUAGES)[number]

function detectLanguage(): Language {
  const saved = getLocalStorage()?.getItem(LANG_KEY)
  if (saved === 'zh' || saved === 'en') return saved
  return 'zh'
}

export function changeLanguage(lang: Language): void {
  getLocalStorage()?.setItem(LANG_KEY, lang)
  void i18n.changeLanguage(lang)
}

void i18n.use(initReactI18next).init({
  resources: {
    zh: { translation: zh },
    en: { translation: en },
  },
  lng: detectLanguage(),
  fallbackLng: 'zh',
  interpolation: { escapeValue: false },
})

export default i18n
