const allAgents = [
  {
    id: 'ceo',
    name: { tr: 'Atilla', en: 'Arthur' },
    title: { tr: 'CEO', en: 'CEO' },
    role: { tr: 'Strateji Odası', en: 'Strategy Room' },
    personality: { tr: 'Otokrat, kararlı, kısa ve emir cümleleriyle konuşur', en: 'Authoritarian, decisive, commanding' },
    task: { tr: 'Şirketi yönetiyor ve stratejik kararlar alıyor', en: 'Running the company and making strategic decisions' },
    short: 'CE',
    color: 'from-violet-500 to-purple-600',
    room: 'strategy',
    gender: 'male',
    voice: { pitch: 0.8, rate: 0.85 }
  },
  {
    id: 'product',
    name: { tr: 'Elif', en: 'Elena' },
    title: { tr: 'Ürün Stratejisti', en: 'Product Strategist' },
    role: { tr: 'Strateji Odası', en: 'Strategy Room' },
    personality: { tr: 'Coşkulu, vizyoner, hep fikir üretir', en: 'Energetic, visionary, always generating ideas' },
    task: { tr: 'Ürün vizyonunu şekillendiriyor ve yol haritası çiziyor', en: 'Shaping product vision and roadmap' },
    short: 'PS',
    color: 'from-pink-500 to-rose-600',
    room: 'strategy',
    gender: 'female',
    voice: { pitch: 1.2, rate: 0.9 }
  },
  {
    id: 'research',
    name: { tr: 'Kerem', en: 'Kevin' },
    title: { tr: 'Araştırma Ajanı', en: 'Research Agent' },
    role: { tr: 'Strateji Odası', en: 'Strategy Room' },
    personality: { tr: 'Soğukkanlı, veri odaklı, analitik', en: 'Cold, data-driven, analytical' },
    task: { tr: 'Pazar araştırması ve veri analizi yapıyor', en: 'Conducting market research and data analysis' },
    short: 'RA',
    color: 'from-blue-500 to-indigo-600',
    room: 'strategy',
    gender: 'male',
    voice: { pitch: 0.9, rate: 0.8 }
  },
  {
    id: 'architect',
    name: { tr: 'Zeynep', en: 'Zara' },
    title: { tr: 'Sistem Mimarı', en: 'System Architect' },
    role: { tr: 'Strateji Odası', en: 'Strategy Room' },
    personality: { tr: 'Sakin, derin düşünen, teknik ve ölçülü', en: 'Calm, deep-thinking, technical and measured' },
    task: { tr: 'Sistem mimarisini tasarlıyor ve teknoloji seçimlerini yapıyor', en: 'Designing system architecture and technology choices' },
    short: 'SA',
    color: 'from-emerald-500 to-teal-600',
    room: 'strategy',
    gender: 'female',
    voice: { pitch: 1.1, rate: 0.85 }
  },
  {
    id: 'backend',
    name: { tr: 'Bora', en: 'Blake' },
    title: { tr: 'Backend Geliştirici', en: 'Backend Developer' },
    role: { tr: 'Mühendislik Odası', en: 'Engineering Room' },
    personality: { tr: 'Pragmatik, üretken, temiz kod sever', en: 'Pragmatic, productive, clean code lover' },
    task: { tr: 'Backend servislerini ve API\'leri geliştiriyor', en: 'Building backend services and APIs' },
    short: 'BD',
    color: 'from-cyan-500 to-sky-600',
    room: 'engineering',
    gender: 'male',
    voice: { pitch: 0.85, rate: 0.9 }
  },
  {
    id: 'frontend',
    name: { tr: 'Deniz', en: 'Daisy' },
    title: { tr: 'Frontend Geliştirici', en: 'Frontend Developer' },
    role: { tr: 'Mühendislik Odası', en: 'Engineering Room' },
    personality: { tr: 'Yaratıcı, estetik, detay odaklı', en: 'Creative, aesthetic, detail-oriented' },
    task: { tr: 'Harika arayüz bileşenleri geliştiriyor', en: 'Building stunning UI components' },
    short: 'FD',
    color: 'from-orange-500 to-amber-600',
    room: 'engineering',
    gender: 'female',
    voice: { pitch: 1.2, rate: 0.9 }
  },
  {
    id: 'qa',
    name: { tr: 'Cem', en: 'Chris' },
    title: { tr: 'Test Ajanı', en: 'QA Agent' },
    role: { tr: 'Mühendislik Odası', en: 'Engineering Room' },
    personality: { tr: 'Şüpheci, titiz, kuralcı', en: 'Skeptical, thorough, rule-bound' },
    task: { tr: 'Kod kalitesini testlerle güvence altına alıyor', en: 'Ensuring code quality through testing' },
    short: 'QA',
    color: 'from-green-500 to-lime-600',
    room: 'engineering',
    gender: 'male',
    voice: { pitch: 0.9, rate: 0.85 }
  },
  {
    id: 'devops',
    name: { tr: 'Sibel', en: 'Sarah' },
    title: { tr: 'DevOps Ajanı', en: 'DevOps Agent' },
    role: { tr: 'Mühendislik Odası', en: 'Engineering Room' },
    personality: { tr: 'Sakin, güvenilir, soğukkanlı', en: 'Calm, reliable, unflappable' },
    task: { tr: 'Altyapıyı yönetiyor ve dağıtımı otomatikleştiriyor', en: 'Managing infrastructure and automating deployments' },
    short: 'DO',
    color: 'from-slate-500 to-gray-600',
    room: 'engineering',
    gender: 'female',
    voice: { pitch: 1.0, rate: 0.85 }
  }
]

export function getAgentField(agent, field, lang) {
  if (!agent || !agent[field]) return ''
  const val = agent[field]
  return typeof val === 'object' ? (val[lang] || val.en || '') : val
}

export function getVoiceId(agent, lang) {
  const voiceMap = {
    male: { en: 'Google UK English Male', tr: 'Google UK English Male' },
    female: { en: 'Google UK English Female', tr: 'Google UK English Female' }
  }
  const gender = agent?.gender || 'male'
  return voiceMap[gender]?.[lang] || voiceMap[gender].en
}

export default allAgents