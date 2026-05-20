import { useState, useEffect, useRef } from 'react'
import allAgents, { getAgentField } from '../data/agents'

const agentLookup = {}
allAgents.forEach(a => { agentLookup[a.id] = a })

const levelColors = {
  info: 'text-cyan-300',
  success: 'text-emerald-400',
  error: 'text-red-400',
  warn: 'text-amber-400',
  cmd: 'text-violet-400',
}

const levelIcons = {
  info: '●',
  success: '✔',
  error: '✘',
  warn: '⚠',
  cmd: '❯',
}

function formatTime(ts) {
  const d = new Date(ts)
  return d.toLocaleTimeString('tr-TR', { hour12: false })
}

function getAgentName(agentId, lang) {
  if (!agentId) return 'agent'
  const agent = agentLookup[agentId]
  return agent ? getAgentField(agent, 'name', lang) : agentId
}

export default function ActivityTerminal({ lang, speakingAgentId, activeProject }) {
  const [lines, setLines] = useState([])
  const [activeAgentIds, setActiveAgentIds] = useState([])
  const terminalRef = useRef(null)
  const prevLenRef = useRef(0)
  const speakingRef = useRef(speakingAgentId)
  speakingRef.current = speakingAgentId

  useEffect(() => {
    const poll = async () => {
      const ids = new Set()
      if (speakingRef.current) ids.add(speakingRef.current)
      try {
        const feedRes = await fetch('/api/command/feed?count=200')
        if (feedRes.ok) {
          const data = await feedRes.json()
          if (Array.isArray(data) && data.length > 0) {
            setLines(data.reverse())
          }
        }
        const actRes = await fetch('/api/command/activity')
        if (actRes.ok) {
          const act = await actRes.json()
          const activeCmds = Array.isArray(act.active) ? act.active : (act.active ? [act.active] : [])
          for (const cmd of activeCmds) {
            if (cmd.activeAgentId) ids.add(cmd.activeAgentId)
          }
          if (Array.isArray(act.recent)) {
            for (const cmd of act.recent) {
              if (cmd.status === 'processing' && cmd.activeAgentId) {
                ids.add(cmd.activeAgentId)
              }
            }
          }
        }
      } catch { }
      setActiveAgentIds(Array.from(ids))
    }
    poll()
    const interval = setInterval(poll, 1500)
    return () => clearInterval(interval)
  }, [])

  useEffect(() => {
    if (terminalRef.current && lines.length > prevLenRef.current) {
      terminalRef.current.scrollTop = terminalRef.current.scrollHeight
    }
    prevLenRef.current = lines.length
  }, [lines.length])

  if (lines.length === 0) {
    const activeNames = activeAgentIds.map(id => getAgentName(id, lang)).filter(Boolean)
    return (
      <div className="bg-[#0d1117] rounded-2xl border border-[#30363d] p-3 font-mono text-xs text-[#8b949e] h-48 flex items-center justify-center select-none">
        <span className="animate-pulse">
          {activeNames.length > 0
            ? (lang === 'tr' ? `${activeNames.join(', ')} çalışıyor...` : `${activeNames.join(', ')} working...`)
            : (lang === 'tr' ? 'Ajanlar bekliyor...' : 'Agents waiting...')}
        </span>
      </div>
    )
  }

  return (
    <div className="bg-[#0d1117] rounded-2xl border border-[#30363d] font-mono text-xs select-none">
      <div className="flex items-center gap-1.5 px-3 py-2 border-b border-[#30363d] bg-[#161b22] rounded-t-2xl">
        <span className="w-2.5 h-2.5 rounded-full bg-[#ff7b72]" />
        <span className="w-2.5 h-2.5 rounded-full bg-[#d2a8ff]" />
        <span className="w-2.5 h-2.5 rounded-full bg-[#a5d6ff]" />
        <span className="text-[#8b949e] ml-2">
          {lang === 'tr' ? 'Sistem Terminali' : 'System Terminal'}
          {activeProject && <span className="text-emerald-400 ml-1.5">({activeProject})</span>}
        </span>
        {activeAgentIds.length > 0 && (
          <span className="ml-auto text-[#7ee787] text-[10px] flex items-center gap-1">
            <span className="w-1.5 h-1.5 rounded-full bg-[#7ee787] animate-pulse" />
            {activeAgentIds.map(id => getAgentName(id, lang)).filter(Boolean).join(', ')}
          </span>
        )}
      </div>
      <div ref={terminalRef} className="h-36 overflow-y-auto p-3 space-y-0.5 scroll-smooth" style={{ scrollBehavior: 'smooth' }}>
        {lines.map((line, i) => (
          <div key={i} className="leading-5 hover:bg-[#161b22]/50 transition-colors rounded px-0.5">
            <span className="text-[#484f58] mr-2">{formatTime(line.timestamp)}</span>
            <span className={`${levelColors[line.level] || 'text-[#c9d1d9]'} mr-1`}>
              {levelIcons[line.level] || '●'}
            </span>
            {line.level === 'cmd' && (
              <span className="text-[#58a6ff] mr-1">{getAgentName(line.agentId, lang)}@agent:~$</span>
            )}
            {line.level !== 'cmd' && line.agentId && (
              <span className="text-[#58a6ff] mr-1">[{getAgentName(line.agentId, lang)}]</span>
            )}
            <span className={`${line.level === 'cmd' ? 'text-[#7ee787]' : line.level === 'error' ? 'text-red-400' : 'text-[#c9d1d9]'}`}>
              {line.text}
            </span>
          </div>
        ))}
      </div>
    </div>
  )
}
