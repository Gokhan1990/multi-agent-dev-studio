import allAgents, { getAgentField } from './agents'
import agents, { routeMessage, getResponse } from './responses'

const componentMap = {
  title: { component: 'MeetingRoom', selector: 'h1', prop: 'className' },
  subtitle: { component: 'MeetingRoom', selector: 'p', prop: 'className' },
  chatArea: { component: 'MeetingRoom', selector: '.transcript-area', prop: 'className' },
  inputArea: { component: 'MeetingRoom', selector: 'input', prop: 'className' },
}

const stylePatterns = [
  { match: /rengi?\s*(?:cyan|mavi|blue|violet|mor|yeşil|green|kırmızı|red|pembe|pink|beyaz|white|siyah|black)/i, type: 'color' },
  { match: /(?:büyüt|genişlet|büyük|large)/i, type: 'size-up' },
  { match: /(?:küçült|daralt|küçük|small)/i, type: 'size-down' },
  { match: /(?:stil|style|font|yazı|tema|theme)/i, type: 'style' },
  { match: /(?:ekle|add|create|yeni|new)/i, type: 'create' },
  { match: /(?:sil|delete|remove|kaldır)/i, type: 'delete' },
]

export function parseCommand(text) {
  const lower = text.toLowerCase()
  const matched = stylePatterns.find(p => p.match.test(lower))
  return {
    raw: text,
    type: matched?.type || 'unknown',
    hasColor: /rengi?/i.test(lower),
    targetColor: (lower.match(/(?:cyan|mavi|blue|violet|mor|yeşil|green|kırmızı|red|pembe|pink|beyaz|white|siyah|black)/i) || [])[0],
    isResize: /(?:büyüt|küçült|genişlet|daralt)/i.test(lower),
    isCreate: /(?:ekle|yap|create|yeni)/i.test(lower),
    isDelete: /(?:sil|kaldır|delete)/i.test(lower),
  }
}

const colorMap = {
  cyan: 'cyan', mavi: 'blue', blue: 'blue',
  violet: 'violet', mor: 'violet',
  yeşil: 'green', green: 'green',
  kırmızı: 'red', red: 'red',
  pembe: 'pink', pink: 'pink',
  beyaz: 'white', white: 'white',
  siyah: 'black', black: 'black',
}

export function resolveStyleChange(parsed) {
  if (parsed.hasColor && parsed.targetColor) {
    const base = colorMap[parsed.targetColor.toLowerCase()] || parsed.targetColor
    return { className: `text-${base}-400`, type: 'text-color' }
  }
  if (parsed.isResize) return { scale: parsed.type === 'size-up' ? 1.1 : 0.9, type: 'scale' }
  return null
}

export function getComponentTarget(text) {
  const lower = text.toLowerCase()
  if (lower.includes('sohbet') || lower.includes('chat') || lower.includes('transcript')) return 'chatArea'
  if (lower.includes('başlık') || lower.includes('title') || lower.includes('başlığı')) return 'title'
  if (lower.includes('alt') || lower.includes('subtitle')) return 'subtitle'
  if (lower.includes('giriş') || lower.includes('input') || lower.includes('yaz')) return 'inputArea'
  return null
}

export function bridgeCommand(userMessage, lang) {
  const { agentIds } = routeMessage(userMessage, 'engineering')
  const isFrontend = agentIds.includes('frontend')
  const parsed = parseCommand(userMessage)
  const componentTarget = getComponentTarget(userMessage)
  const styleChange = resolveStyleChange(parsed)

  return {
    isFrontendTask: isFrontend,
    agents: agentIds.map(id => ({
      id,
      name: getAgentField(allAgents.find(a => a.id === id), 'name', lang),
      response: getResponse(id, 'engineering', userMessage, lang),
    })),
    parsed,
    componentTarget: componentTarget ? componentMap[componentTarget] : null,
    styleChange,
  }
}

export function formatChangeSummary(parsed) {
  if (parsed.hasColor && parsed.targetColor) {
    const colorName = parsed.targetColor
    return `Renk değişikliği → ${colorName}`
  }
  if (parsed.isResize) return parsed.type === 'size-up' ? 'Alan büyütülecek' : 'Alan küçültülecek'
  if (parsed.isCreate) return 'Yeni bileşen eklenecek'
  if (parsed.isDelete) return 'Bileşen kaldırılacak'
  return 'Komut işleniyor...'
}

export default {
  parseCommand,
  resolveStyleChange,
  getComponentTarget,
  bridgeCommand,
  formatChangeSummary,
  componentMap,
}
