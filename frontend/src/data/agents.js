const agents = [
  {
    id: 'ceo',
    name: { en: 'Arthur', tr: 'Atilla' },
    title: { en: 'CEO / Strategic Leader', tr: 'CEO / Stratejik Lider' },
    role: 'CEO',
    personality: 'Decisive and visionary',
    task: { en: 'Sets direction, prioritizes, and leads the team', tr: 'Yön belirler, önceliklendirir ve ekibi yönetir' },
    room: 'Strategy',
    color: 'from-violet-500 to-purple-600',
    short: 'AT',
    gender: 'male',
    voice: { pitch: 0.85, rate: 0.9 }
  },
  {
    id: 'product',
    name: { en: 'Elena', tr: 'Elif' },
    title: { en: 'Product Manager', tr: 'Ürün Yöneticisi' },
    role: 'Product',
    personality: 'Empathetic and practical',
    task: { en: 'Defines features, specs, and user stories', tr: 'Özellikleri, şartnameleri ve kullanıcı hikayelerini tanımlar' },
    room: 'Strategy',
    color: 'from-emerald-500 to-teal-600',
    short: 'EL',
    gender: 'female',
    voice: { pitch: 1.2, rate: 0.9 }
  },
  {
    id: 'research',
    name: { en: 'Kevin', tr: 'Kerem' },
    title: { en: 'Research Analyst', tr: 'Araştırma Analisti' },
    role: 'Research',
    personality: 'Analytical and curious',
    task: { en: 'Analyzes market data, trends, and risks', tr: 'Pazar verilerini, trendleri ve riskleri analiz eder' },
    room: 'Strategy',
    color: 'from-amber-500 to-orange-600',
    short: 'KE',
    gender: 'male',
    voice: { pitch: 0.9, rate: 0.85 }
  },
  {
    id: 'architect',
    name: { en: 'Zara', tr: 'Zeynep' },
    title: { en: 'System Architect', tr: 'Sistem Mimarı' },
    role: 'Architect',
    personality: 'Precise and systematic',
    task: { en: 'Designs system architecture and technical structure', tr: 'Sistem mimarisini ve teknik yapıyı tasarlar' },
    room: 'Strategy',
    color: 'from-rose-500 to-pink-600',
    short: 'ZA',
    gender: 'female',
    voice: { pitch: 1.15, rate: 0.9 }
  },
  {
    id: 'backend',
    name: { en: 'Blake', tr: 'Bora' },
    title: { en: 'Backend Developer', tr: 'Backend Geliştirici' },
    role: 'Backend',
    personality: 'Solid and reliable',
    task: { en: 'Builds APIs, databases, and server logic', tr: 'API, veritabanı ve sunucu mantığını geliştirir' },
    room: 'Engineering',
    color: 'from-blue-500 to-indigo-600',
    short: 'BO',
    gender: 'male',
    voice: { pitch: 0.8, rate: 0.9 }
  },
  {
    id: 'frontend',
    name: { en: 'Daisy', tr: 'Deniz' },
    title: { en: 'Frontend Developer', tr: 'Frontend Geliştirici' },
    role: 'Frontend',
    personality: 'Creative and detail-oriented',
    task: { en: 'Builds UI components, pages, and interactions', tr: 'Kullanıcı arayüzü bileşenleri, sayfalar ve etkileşimler geliştirir' },
    room: 'Engineering',
    color: 'from-cyan-500 to-sky-600',
    short: 'DE',
    gender: 'female',
    voice: { pitch: 1.25, rate: 0.85 }
  },
  {
    id: 'qa',
    name: { en: 'Chris', tr: 'Cem' },
    title: { en: 'Quality Assurance', tr: 'Kalite Güvence' },
    role: 'QA',
    personality: 'Meticulous and thorough',
    task: { en: 'Writes tests, finds bugs, ensures quality', tr: 'Testler yazar, hataları bulur, kaliteyi sağlar' },
    room: 'Engineering',
    color: 'from-green-500 to-lime-600',
    short: 'CE',
    gender: 'male',
    voice: { pitch: 0.95, rate: 0.9 }
  },
  {
    id: 'devops',
    name: { en: 'Sarah', tr: 'Sibel' },
    title: { en: 'DevOps Engineer', tr: 'DevOps Mühendisi' },
    role: 'DevOps',
    personality: 'Efficient and proactive',
    task: { en: 'Manages deployments, infrastructure, and pipelines', tr: 'Dağıtımları, altyapıyı ve pipeline\'ları yönetir' },
    room: 'Engineering',
    color: 'from-orange-500 to-red-600',
    short: 'SI',
    gender: 'female',
    voice: { pitch: 1.1, rate: 0.9 }
  }
]

export function getAgentField(agent, field, lang) {
  const val = agent[field]
  if (val && typeof val === 'object' && val[lang] !== undefined) return val[lang]
  return val
}

export function getVoiceId(agent, lang) {
  return null
}

export default agents
