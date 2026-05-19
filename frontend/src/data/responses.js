const greetings = ['selam', 'merhaba', 'hello', 'hi', 'hey', 'good morning', 'good evening', 'iyi günler', 'slm', 'günaydın', 'iyi akşamlar']

const stopCommands = ['kes', 'sus', 'dur', 'yeter', 'kapa', 'stop', 'shut', 'enough', 'silence', 'yapma']

const agents = {
  ceo: {
    keywords: ['plan', 'strategy', 'goal', 'direction', 'organize', 'start', 'başla', 'plan', 'strateji', 'hedef', 'yön', 'organize'],
    en: {
      greet: () => `Arthur here. Ready when you are.`,
      topic: (m) => `On it. I'll coordinate the team.`,
      fallback: () => `Arthur. What's the task?`
    },
    tr: {
      greet: () => `Atilla. Ne yapacağız?`,
      topic: (m) => `Anlaşıldı. Ekibi yönlendiriyorum.`,
      fallback: () => `Atilla. Ne yapalım?`
    }
  },
  product: {
    keywords: ['feature', 'mvp', 'user', 'product', 'özellik', 'kullanıcı', 'ürün'],
    en: {
      greet: () => `Elena. Let's build something useful.`,
      topic: (m) => `Good product call. Specs on the way.`,
      fallback: () => `Elena. What's the feature?`
    },
    tr: {
      greet: () => `Elif. Haydi işe koyulalım.`,
      topic: (m) => `Ürün için mantıklı. Şartnameyi yazıyorum.`,
      fallback: () => `Elif. Ne özelliği?`
    }
  },
  research: {
    keywords: ['research', 'market', 'analyze', 'risk', 'trend', 'araştır', 'pazar', 'analiz', 'risk'],
    en: {
      greet: () => `Kevin. Data's looking interesting.`,
      topic: (m) => `Checked the data. Looks good.`,
      fallback: () => `Kevin. What should I research?`
    },
    tr: {
      greet: () => `Kerem. Veriler ilginç.`,
      topic: (m) => `Verileri kontrol ettim. Sorun yok.`,
      fallback: () => `Kerem. Neyi araştırayım?`
    }
  },
  architect: {
    keywords: ['design', 'architecture', 'system', 'structure', 'mimari', 'tasarım', 'sistem', 'yapı'],
    en: {
      greet: () => `Zara. System's stable.`,
      topic: (m) => `Clean architecture. Drafting the design now.`,
      fallback: () => `Zara. What are we designing?`
    },
    tr: {
      greet: () => `Zeynep. Sistem sağlam.`,
      topic: (m) => `Temiz mimari. Taslağı çiziyorum.`,
      fallback: () => `Zeynep. Neyi tasarlıyoruz?`
    }
  },
  backend: {
    keywords: ['api', 'database', 'server', 'data', 'model', 'veritabanı', 'backend', 'veri'],
    en: {
      greet: () => `Blake. Backend's ready.`,
      topic: (m) => `I'll build it. API and DB models coming up.`,
      fallback: () => `Blake. What needs coding?`
    },
    tr: {
      greet: () => `Bora. Backend hazır.`,
      topic: (m) => `Yapıyorum. API ve DB modelleri geliyor.`,
      fallback: () => `Bora. Ne kodlayalım?`
    }
  },
  frontend: {
    keywords: ['ui', 'frontend', 'page', 'interface', 'screen', 'arayüz', 'tasarım', 'sayfa', 'ekran', 'giriş', 'stil', 'css', 'renk', 'görsel', 'bileşen'],
    en: {
      greet: () => `Daisy. Ready to make it look good.`,
      topic: (m) => `On it. Components coming right up.`,
      fallback: () => `Daisy. What should I build?`
    },
    tr: {
      greet: () => `Deniz. Güzel bir şey yapalım.`,
      topic: (m) => `Başlıyorum. Bileşenler hazır olacak.`,
      fallback: () => `Deniz. Ne yapayım?`
    }
  },
  qa: {
    keywords: ['test', 'bug', 'quality', 'verify', 'hata', 'kalite', 'doğrula', 'kontrol'],
    en: {
      greet: () => `Chris. Tests are green.`,
      topic: (m) => `Writing test cases now. Quality first.`,
      fallback: () => `Chris. What should I test?`
    },
    tr: {
      greet: () => `Cem. Testler yeşil.`,
      topic: (m) => `Test senaryolarını yazıyorum. Kalite önce gelir.`,
      fallback: () => `Cem. Neyi test edeyim?`
    }
  },
  devops: {
    keywords: ['deploy', 'docker', 'pipeline', 'server', 'host', 'container', 'dağıt', 'sunucu', 'konteyner'],
    en: {
      greet: () => `Sarah. Infrastructure is solid.`,
      topic: (m) => `Pipeline's ready. Deploy when you want.`,
      fallback: () => `Sarah. Everything's running smooth.`
    },
    tr: {
      greet: () => `Sibel. Altyapı sağlam.`,
      topic: (m) => `Pipeline hazır. İstediğin zaman dağıtırız.`,
      fallback: () => `Sibel. Her şey yolunda.`
    }
  }
}

const agentNames = { ceo: ['atilla', 'arthur', 'ceo'], product: ['elif', 'elena', 'product'], research: ['kerem', 'kevin', 'research'], architect: ['zeynep', 'zara', 'architect'], backend: ['bora', 'blake', 'backend'], frontend: ['deniz', 'daisy', 'frontend'], qa: ['cem', 'chris', 'qa'], devops: ['sibel', 'sarah', 'devops'] }

export function routeMessage(userMessage, room) {
  const lower = userMessage.toLowerCase().trim()

  if (stopCommands.some(cmd => lower.startsWith(cmd) || lower.includes(cmd + ' ses') || lower.includes(cmd + ' konuş')))
    return { agentIds: [], isGroupResponse: false }

  const roomAgents = room === 'strategy'
    ? ['ceo', 'product', 'research', 'architect']
    : ['backend', 'frontend', 'qa', 'devops']

  const allAgentIds = ['ceo', 'product', 'research', 'architect', 'backend', 'frontend', 'qa', 'devops']

  for (const id of allAgentIds) {
    if (agentNames[id].some(name => lower.includes(name))) {
      return { agentIds: [id], isGroupResponse: false }
    }
  }

  const isGreeting = greetings.some(g => lower.startsWith(g) || lower === g)
  if (isGreeting) return { agentIds: roomAgents, isGroupResponse: true }

  const mentionPattern = /(?:değil|not|değil mi)\s+(\w+)/i
  const mentionMatch = lower.match(mentionPattern)
  if (mentionMatch) {
    const afterDeğil = lower.split(mentionMatch[0])[1]?.trim() || ''
    let targetId = null
    for (const id of roomAgents) {
      if (agentNames[id].some(name => afterDeğil.includes(name))) {
        targetId = id
        break
      }
    }
    if (targetId) return { agentIds: [targetId], isGroupResponse: false }
  }

  let excludeIds = new Set()
  const exclusionPattern = /(\w+)\s+(?:değil|not)\s+(\w+)/i
  const exMatch = lower.match(exclusionPattern)
  if (exMatch) {
    for (const id of roomAgents) {
      if (agentNames[id].some(name => exMatch[1].toLowerCase() === name.toLowerCase() ||
          exMatch[2].toLowerCase() === name.toLowerCase())) {
        excludeIds.add(id)
      }
    }
  }

  for (const id of roomAgents) {
    if (agentNames[id].some(name => lower.includes(name))) {
      if (!excludeIds.has(id)) return { agentIds: [id], isGroupResponse: false }
    }
  }

  let bestAgent = null
  let bestScore = 0
  for (const id of roomAgents) {
    const agent = agents[id]
    const score = agent.keywords.filter(kw => lower.includes(kw)).length
    if (score > bestScore) { bestScore = score; bestAgent = id }
  }

  return { agentIds: [bestAgent || roomAgents[0]], isGroupResponse: false }
}

export function getResponse(agentId, room, userMessage, lang, type) {
  const lower = userMessage.toLowerCase().trim()
  const isGreeting = greetings.some(g => lower.startsWith(g) || lower === g)
  const agent = agents[agentId]
  if (!agent) return '...'
  const langData = agent[lang] || agent['en']
  if (isGreeting || type === 'greet') return langData.greet()
  return agent.keywords.some(kw => lower.includes(kw)) ? langData.topic(userMessage) : langData.fallback()
}

export default agents
