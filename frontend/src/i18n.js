const tr = {
  title: 'AI Ajan Toplantı Odası',
  subtitle: 'Çoklu ajan canlı işbirliği paneli',
  strategyRoom: '🧠 Strateji Odası',
  engineeringRoom: '⚙️ Mühendislik Odası',
  mic: 'Mikrofon',
  stop: 'Durdur',
  speaking: 'Konuşuyor...',
  noSpeech: 'Ses tanıma desteklenmiyor',
  captions: 'Altyazı',
}

const en = {
  title: 'AI Agent Meeting Room',
  subtitle: 'Multi-agent live collaboration dashboard',
  strategyRoom: '🧠 Strategy Room',
  engineeringRoom: '⚙️ Engineering Room',
  mic: 'Microphone',
  stop: 'Stop',
  speaking: 'Speaking...',
  noSpeech: 'Speech recognition not supported',
  captions: 'Captions',
}

export function t(lang, key) {
  const dict = lang === 'tr' ? tr : en
  return dict[key] || key
}

export const languages = [
  { code: 'en', label: 'EN' },
  { code: 'tr', label: 'TR' },
]
